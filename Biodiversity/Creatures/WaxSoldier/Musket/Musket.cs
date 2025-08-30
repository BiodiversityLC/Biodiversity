using Biodiversity.Core.Integration;
using Biodiversity.Items;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class Musket : BiodiverseItem
{
    private static readonly int ReelingUpAnimatorHash = Animator.StringToHash("reelingUp");
    private static readonly int ShovelHitAnimatorHash = Animator.StringToHash("shovelHit");

    #region Unity Inspector Variables
    [Header("Audio")]
    [SerializeField] private AudioSource shootAudioSource;
    [SerializeField] private AudioSource otherAudioSource;
    
    [SerializeField] private AudioClip shootSfx;
    [SerializeField] private AudioClip shootFailSfx;
    [SerializeField] private AudioClip turnSafetyOnSfx;
    [SerializeField] private AudioClip turnSafetyOffSfx;
    [SerializeField] private AudioClip reelUpSfx;
    [SerializeField] private AudioClip swingSfx;
    [SerializeField] private AudioClip stabIntoFleshSfx;
    
    [Header("Colliders")]
    [SerializeField] private BoxCollider bayonetCollider;

    [Header("Transforms")]
    [SerializeField] public Transform bayonetTip;
    [SerializeField] public Transform muzzleTip;
    [SerializeField] public Transform bulletRayOrigin;
    
    [Header("Controllers")]
    [SerializeField] public WaxSoldierBayonetAttackPhysics bayonetAttackPhysics;

    [Header("Debug")]
    public bool enableDebugVisuals = false;
    #endregion
    
    private enum DamageType
    {
        Player,
        IHittable
    }

    private enum HitResult
    {
        None,
        Target,
        Scenery,
    }

    private enum AttackMode
    {
        Gun,
        Bayonet
    }

    private enum AttackFailureReason
    {
        None,
        NotHeld,
        NeedsReloading,
        SafetyIsOn,
        AlreadyPerformingAttackAction
    }
    
    private readonly PlayerCooldownTracker _playerDamageCooldowns = new();

    private RaycastHit[] _bulletHitBuffer;
    private Collider[] _bayonetHitBuffer;

    private CachedValue<Vector3> _bayonetColliderHalfExtents;

    private Coroutine _reelingUpCoroutine;
    private Coroutine _shootingCoroutine;

    private AttackMode _currentAttackMode;

    private Vector3 _itemPositionOffset;
    private Vector3 _itemRotationOffset;
    
    private const int bulletHitId = 8832676;
    private const int bayonetHitId = 8832677;
    private const int bulletHitBufferCapacity = 10;
    private const int bayonetHitBufferCapacity = 25;

    private float _bulletRadius;
    private float _bulletMaxDistance;
    
    [Tooltip("How much the AI's vertical aim is corrected. 0 = no help, 1 = perfect Y-axis aimbot.")]
    [Range(0f, 1f)]
    public float verticalAimAssist = 0.95f;

    public NetworkVariable<int> currentAmmo { get; private set; } = new(1);
    private int _bulletHitMask;
    private int _bayonetHitMask;
    private int _maxAmmo;

    private readonly NetworkVariable<bool> _isSafetyOn = new();
    private static bool _hasRegisteredImperiumInsights;
    private bool _isPerformingAttackAction;
    private bool _isHoldingButton;
    private bool _isHeldByWaxSoldier;
    private bool _piercingRoundsEnabled;
    
    private void Awake()
    {
        _bulletHitBuffer = new RaycastHit[bulletHitBufferCapacity];
        _bayonetHitBuffer = new Collider[bayonetHitBufferCapacity];
        
        _bayonetHitMask = 1084754248; // This is the mask used by the vanilla shovel, see Util.VanillaLayersUtil for more details
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
        
        _currentAttackMode = AttackMode.Gun;
        _piercingRoundsEnabled = true;
        _bulletRadius = 0.1f;
        _bulletMaxDistance = 200f;
        _maxAmmo = 1;
        
        _itemPositionOffset = new Vector3(0.02f, 0.65f, -0.05f);
        _itemRotationOffset = new Vector3(265f, 6f, 270f);
    }

    private void OnDisable()
    {
        DebugShapeVisualizer.Clear(this);
    }

    public override void Start()
    {
        base.Start();

        _bayonetColliderHalfExtents = new CachedValue<Vector3>(() => bayonetCollider.size * 0.5f);
        bayonetAttackPhysics.bayonetCollider = bayonetCollider;
        
        // todo: check the vanilla shotgun's bullet mask, cuz the musket bullet can go through doors rn which is bad
        _bulletHitMask = StartOfRound.Instance.collidersRoomMaskDefaultAndPlayers | (1 << LayerMask.NameToLayer("Enemies"));
        _isSafetyOn.Value = false;

        if (ImperiumIntegration.IsLoaded && !_hasRegisteredImperiumInsights)
        {
            Imperium.API.Visualization.InsightsFor<Musket>()
                .UnregisterInsight("Used Up")
                .UnregisterInsight("Cooldown")
                .UnregisterInsight("Location")

                .RegisterInsight("Ammo", item => item.currentAmmo.Value.ToString())
                .RegisterInsight("Trigger Safety", _ => _isSafetyOn.Value ? "On" : "Off")
                .RegisterInsight("Attack Mode", item => item._currentAttackMode == AttackMode.Gun
                    ? "Shoot"
                    : "Stab");
            
            _hasRegisteredImperiumInsights = true;
        }
    }
    
    public override void LateUpdate()
    {
        itemProperties.positionOffset = _isHeldByWaxSoldier ? Vector3.zero : _itemPositionOffset;
        itemProperties.rotationOffset = _isHeldByWaxSoldier ? Vector3.zero : _itemRotationOffset;
        
        base.LateUpdate();
    }

    private bool CanAttack(out AttackFailureReason failureReason)
    {
        failureReason = AttackFailureReason.None;
        
        if (!isHeld || (!isHeldByPlayer && !isHeldByEnemy))
        {
            failureReason = AttackFailureReason.NotHeld;
            return false;
        }

        if (_isPerformingAttackAction)
        {
            failureReason = AttackFailureReason.AlreadyPerformingAttackAction;
            return false;
        }

        switch (_currentAttackMode)
        {
            case AttackMode.Bayonet:
                return true;
            
            case AttackMode.Gun:
                if (!_isHoldingButton)
                {
                    return false;
                }
                
                if (currentAmmo.Value <= 0)
                {
                    failureReason = AttackFailureReason.NeedsReloading;
                    return false;
                }
                
                if (_isSafetyOn.Value)
                {
                    failureReason = AttackFailureReason.SafetyIsOn;
                    return false;
                }

                return true;
            
            default:
                return false;
        }
    }

    private IEnumerator Shoot()
    {
        // LogVerbose($"In {nameof(Shoot)}.");
        _isPerformingAttackAction = true;
        
        PlayRandomAudioClipTypeServerRpc(nameof(shootSfx), nameof(shootAudioSource), true, audibleByEnemies: true);
        yield return new WaitForSeconds(0.09f); // The actual gunshot happens 0.09 seconds into the shoot audio clip
        PerformBulletLogic();
        _isPerformingAttackAction = false;
    }

    private void PerformBulletLogic()
    {
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value - 1, 0, _maxAmmo);
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
        
        Ray bulletRay;

        if (false && isHeldByPlayer)
        {
            bulletRay = new Ray(
                localPlayer.gameplayCamera.transform.position - localPlayer.gameplayCamera.transform.up * 0.45f,
                localPlayer.gameplayCamera.transform.forward);
        }
        else
        {
            Vector3 bulletOrigin = bulletRayOrigin.position;
            Vector3 finalDirection;

            if (enemyHeldBy && enemyHeldBy.targetPlayer)
            {
                Vector3 naturalAimDirection = bulletRayOrigin.forward;
                
                PlayerControllerB targetPlayer = enemyHeldBy.targetPlayer;
                Vector3 targetCentre = targetPlayer.gameplayCamera.transform.position;
                
                Vector3 correctedTargetPoint = new(targetCentre.x, bulletOrigin.y, targetCentre.z);
                Vector3 perfectAimDirection = (correctedTargetPoint - bulletOrigin).normalized;
                
                finalDirection = Vector3.Slerp(naturalAimDirection, perfectAimDirection, verticalAimAssist);
            }
            else
            {
                finalDirection = bulletRayOrigin.forward;
            }

            bulletRay = new Ray(bulletOrigin, finalDirection);
        }

        int hitCount = Physics.SphereCastNonAlloc(bulletRay, _bulletRadius, _bulletHitBuffer, _bulletMaxDistance, _bulletHitMask,
            QueryTriggerInteraction.Collide);

        if (hitCount == 0)
        {
            if (enableDebugVisuals) DrawBulletDebug(bulletRay, null);
            return;
        }

        List<int> successfulHitIndicies = enableDebugVisuals ? new List<int>(hitCount) : null;

        for (int i = 0; i < hitCount; i++)
        {
            // Find the closest valid hit without sorting the whole array
            int closestHitIndex = FindClosestValidHit(hitCount);
            if (closestHitIndex == -1) break; // No valid targets found
            
            RaycastHit hit = _bulletHitBuffer[closestHitIndex];
            Transform hitTransform = hit.transform;

            // Mark this hit as processed
            _bulletHitBuffer[closestHitIndex].distance = float.MaxValue;

            HitResult hitResult = HitResult.None;

            if (hitTransform.TryGetComponent(out PlayerControllerB player))
            {
                ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(player);
                if (!_playerDamageCooldowns.IsOnCooldown(playerClientId))
                {
                    int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.Player);
                    DamagePlayerServerRpc(playerClientId, bulletDamage, CauseOfDeath.Gunshots);
                    _playerDamageCooldowns.Start(playerClientId, 0.2f);
                    
                    LogVerbose($"Musket bullet dealt {bulletDamage} damage to {player.playerUsername}.");
                    hitResult = HitResult.Target;
                }
            }
            else if (hitTransform.TryGetComponent(out IHittable iHittable))
            {
                int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.IHittable);
                iHittable.Hit(bulletDamage, bulletRay.origin, playerHeldBy, true, bulletHitId);
                LogVerbose($"Musket bullet dealt {bulletDamage} damage to {iHittable}.");
                hitResult = HitResult.Target;
            }
            else
            {
                LogVerbose("Musket bullet hit scenery.");
                hitResult = HitResult.Scenery;
            }
            
            if (hitResult is HitResult.Target or HitResult.Scenery)
            {
                successfulHitIndicies?.Add(closestHitIndex);
                if (hitResult == HitResult.Target && _piercingRoundsEnabled)
                    continue;
            }
            
            break;
        }

        if (enableDebugVisuals)
        {
            DrawBulletDebug(bulletRay, successfulHitIndicies);
        }
    }

    private IEnumerator ReelUpBayonet()
    {
        LogVerbose("Reeling up bayonet...");
        PlayerControllerB playerUsingBayonet = playerHeldBy; // Needed so we can fix their animations if they drop the musket, which makes `playerHeldBy = null`
        
        playerUsingBayonet.activatingItem = true;
        playerUsingBayonet.twoHanded = true;
        playerUsingBayonet.playerBodyAnimator.ResetTrigger(ShovelHitAnimatorHash);
        playerUsingBayonet.playerBodyAnimator.SetBool(ReelingUpAnimatorHash, true);
        
        PlayRandomAudioClipTypeServerRpc(nameof(reelUpSfx), nameof(otherAudioSource), true, true, true, true);

        yield return new WaitForSeconds(0.35f);
        yield return new WaitUntil(() => !_isHoldingButton || !isHeld);
        
        playerUsingBayonet.playerBodyAnimator.SetBool(ReelingUpAnimatorHash, false);
        if (!isHeld)
        {
            LogVerbose("Musket has been dropped, cancelling bayonet stab.");
            _isPerformingAttackAction = false;
            yield break;
        }
        
        LogVerbose("Swinging bayonet...");
        PlayRandomAudioClipTypeServerRpc(nameof(swingSfx), nameof(otherAudioSource), true, true, true, true);
        playerUsingBayonet.UpdateSpecialAnimationValue(true, (short)playerUsingBayonet.transform.localEulerAngles.y, 0.4f);
        
        yield return new WaitForSeconds(0.13f);
        yield return new WaitForEndOfFrame();
        
        if (!isHeld)
        {
            LogVerbose("Musket has been dropped, cancelling bayonet stab.");
            _isPerformingAttackAction = false;
            yield break;
        }
        
        playerUsingBayonet.activatingItem = false;
        playerUsingBayonet.twoHanded = false;
        HitBayonet();
        
        yield return new WaitForSeconds(0.3f);
        _isPerformingAttackAction = false;
    }

    public void HitBayonet()
    {
        LogVerbose($"In {nameof(HitBayonet)}");
        
        int hitCount = Physics.OverlapBoxNonAlloc(bayonetCollider.transform.position, _bayonetColliderHalfExtents.Value,
            _bayonetHitBuffer, Quaternion.identity, _bayonetHitMask, QueryTriggerInteraction.Collide);
        
        if (hitCount == 0) return;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = _bayonetHitBuffer[i];

            if (collider.CompareTag("Player") && collider.transform.TryGetComponent(out PlayerControllerB player))
            {
                if (isHeldByPlayer && playerHeldBy == player) continue;
                
                ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(player);
                if (_playerDamageCooldowns.IsOnCooldown(playerClientId)) continue;
                
                DamagePlayerServerRpc(playerClientId, 100, CauseOfDeath.Stabbing);
                _playerDamageCooldowns.Start(playerClientId, 0.2f);
                
                continue;
            }

            if (collider.transform.TryGetComponent(out IHittable iHittable))
            {
                iHittable.Hit(4, bayonetTip.forward, playerHeldBy, true, bayonetHitId);
            }
        }
    }
    
    private int FindClosestValidHit(int hitCount)
    {
        int closestIndex = -1;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _bulletHitBuffer[i];

            if (hit.distance <= 0.001f) continue;
            if (hit.distance >= minDistance) continue;

            // Check for self-collision
            if (isHeldByPlayer && hit.transform.GetComponent<PlayerControllerB>() == playerHeldBy) continue;
            if (isHeldByEnemy && hit.transform.GetComponentInParent<EnemyAI>() == enemyHeldBy) continue;

            minDistance = hit.distance;
            closestIndex = i;
        }
        
        return closestIndex;
    }
    
    public void Reload()
    {
        LogVerbose("Reloading...");
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value + 1, 0, _maxAmmo);
    }

    private int CalculateNormalizedBulletDamage(float bulletTravelDistance, DamageType damageType)
    {
        return 110;
    }

    internal void OnGrabbedByWaxSoldier(EnemyAI waxSoldier)
    {
        LogVerbose("Got grabbed by a WaxSoldier.");
        _isHeldByWaxSoldier = true;
        bayonetCollider.isTrigger = true;
        GrabItemFromEnemy(waxSoldier);
    }

    internal void OnDroppedByWaxSoldier()
    {
        LogVerbose("Got dropped by a WaxSoldier.");
        _isHeldByWaxSoldier = false;
        bayonetCollider.isTrigger = false;
        DiscardItemFromEnemy();
    }

    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    private void DamagePlayerServerRpc(ulong playerId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, Vector3 force = default)
    {
        DamagePlayerClientRpc(playerId, damage, causeOfDeath, force);
    }

    [ClientRpc]
    private void DamagePlayerClientRpc(ulong playerId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, Vector3 force = default)
    {
        PlayerControllerB player = PlayerUtil.GetPlayerFromClientId(playerId);
        player.DamagePlayer(damage, true, true, causeOfDeath, 0, false, force);
    }
    #endregion

    public void SetupShoot()
    {
        if (_shootingCoroutine != null) StopCoroutine(_shootingCoroutine);
        _shootingCoroutine = null;
        _isPerformingAttackAction = true;
        _shootingCoroutine = StartCoroutine(Shoot());
    }

    private void DrawBulletDebug(Ray ray, List<int> successfulHitIndicies)
    {
        // 1). Clear previous drawings
        DebugShapeVisualizer.Clear(this);

        Color translucentYellow = Color.yellow;
        translucentYellow.a = 0.5f;
        Color translucentRed = Color.red;
        translucentRed.a = 0.5f;
        
        // 2). Draw the starting sphere from where the bullet originated
        DebugShapeVisualizer.DrawSphere(this, ray.origin, _bulletRadius, translucentYellow);

        // 3). Draw all hit points and the corresponding bullet trajectory
        if (successfulHitIndicies is { Count: > 0 })
        {
            foreach (int i in successfulHitIndicies)
            {
                DebugShapeVisualizer.DrawSphere(this, _bulletHitBuffer[i].point, _bulletRadius, translucentRed);
                DebugShapeVisualizer.DrawLine(this, ray.origin, _bulletHitBuffer[i].point, Color.red);
            }
        }
        
        // 4). Draw the raw trajectory line
        Vector3 endPoint = ray.origin + ray.direction * _bulletMaxDistance;
        DebugShapeVisualizer.DrawLine(this, ray.origin, endPoint, Color.blue);
    }
    
    protected override void CollectAudioClipsAndSources()
    {
        base.CollectAudioClipsAndSources();
        
        AudioSources.Add(nameof(shootAudioSource), shootAudioSource);
        AudioSources.Add(nameof(otherAudioSource), otherAudioSource);
        
        AudioClips.Add(nameof(shootSfx), [shootSfx]);
        AudioClips.Add(nameof(shootFailSfx), [shootFailSfx]);
        AudioClips.Add(nameof(turnSafetyOnSfx), [turnSafetyOnSfx]);
        AudioClips.Add(nameof(turnSafetyOffSfx), [turnSafetyOffSfx]);
        AudioClips.Add(nameof(reelUpSfx), [reelUpSfx]);
        AudioClips.Add(nameof(swingSfx), [swingSfx]);
        AudioClips.Add(nameof(stabIntoFleshSfx), [stabIntoFleshSfx]);
    }
    
    protected override string GetLogPrefix()
    {
        base.GetLogPrefix();
        return $"[Musket {BioId}]";
    }

    #region Abstract Item Class Event Functions
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        _isHoldingButton = buttonDown;
        
        if (!playerHeldBy) return;

        bool canAttack = CanAttack(out AttackFailureReason failureReason);
        switch (_currentAttackMode)
        {
            case AttackMode.Gun:
            {
                if (canAttack)
                {
                    SetupShoot();
                    break;
                }
                
                LogVerbose($"Failed to shoot due to {failureReason}.");

                if (failureReason is AttackFailureReason.NeedsReloading or AttackFailureReason.SafetyIsOn)
                {
                    PlayRandomAudioClipTypeServerRpc(nameof(shootFailSfx), nameof(otherAudioSource), audibleByEnemies: isHeldByPlayer);
                    if (failureReason is AttackFailureReason.NeedsReloading) Reload();
                }
                
                break;
            }

            case AttackMode.Bayonet:
            {
                if (canAttack)
                {
                    if (_reelingUpCoroutine != null) StopCoroutine(_reelingUpCoroutine);
                    _reelingUpCoroutine = null;
                    _isPerformingAttackAction = true;
                    _reelingUpCoroutine = StartCoroutine(ReelUpBayonet());
                    break;
                }
                
                LogVerbose($"Failed to swing bayonet due to {failureReason}.");

                break;
            }
        }
    }

    public override void ItemInteractLeftRight(bool right)
    {
        base.ItemInteractLeftRight(right);
        if (_isPerformingAttackAction) return;
        if (right)
        {
            _currentAttackMode = _currentAttackMode == AttackMode.Gun ? AttackMode.Bayonet : AttackMode.Gun;
            LogVerbose($"Changed attack mode to {_currentAttackMode}.");
        }
        else
        {
            string clipToPlay = _isSafetyOn.Value ? nameof(turnSafetyOffSfx) : nameof(turnSafetyOnSfx);
            _isSafetyOn.Value = !_isSafetyOn.Value;
            PlayRandomAudioClipTypeServerRpc(clipToPlay, nameof(otherAudioSource), true, true, true);
        }
    }

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();
        if (!IsOwner)
        {
            LogWarning($"{nameof(GetItemDataToSave)} called on a client which doesn't own it.");
        }
        
        return currentAmmo.Value;
    }

    public override void LoadItemSaveData(int saveData)
    {
        base.LoadItemSaveData(saveData);
        if (!IsOwner)
        {
            LogWarning($"{nameof(LoadItemSaveData)} called on a client which doesn't own it.");
            return;
        }
        
        currentAmmo.Value = saveData;
    }

    public override void EquipItem()
    {
        base.EquipItem();
        playerHeldBy.equippedUsableItemQE = true;
    }

    public override void DiscardItem()
    {
        playerHeldBy.equippedUsableItemQE = false;
        base.DiscardItem();
    }
    #endregion
}