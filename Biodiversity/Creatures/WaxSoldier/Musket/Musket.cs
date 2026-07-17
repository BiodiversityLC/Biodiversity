// using Biodiversity.Core.Integration;
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
    private static readonly int HoldLungAnimatorHash = Animator.StringToHash("HoldLung");

    #region Unity Inspector Variables
#pragma warning disable 0649
    [Tooltip("How much the AI's vertical aim is corrected. 0 = no help, 1 = perfect Y-axis aimbot.")]
    [Range(0f, 1f)]
    public float verticalAimAssist = 1f;

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

    [Header("Particles")]
    [SerializeField] private ParticleSystem bulletParticles;

    [Header("Controllers")]
    [SerializeField] public WaxSoldierBayonetAttackPhysics bayonetAttackPhysics;

    [Header("Debug")]
    public bool enableDebugVisuals = false;
#pragma warning restore 0649
    #endregion

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

    private readonly NetworkVariable<int> _currentAttackMode = new(
        (int)AttackMode.Gun,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    private AttackMode CurrentAttackMode => (AttackMode)_currentAttackMode.Value;

    private static readonly Vector3 _gunModeItemPositionOffset = new(0.02f, 0.65f, -0.05f);
    private static readonly Vector3 _gunModeItemRotationOffset = new(265f, 6f, 270f);
    private static readonly Vector3 _bayonetModeItemPositionOffset = new(0f, -0.1f, 0f);
    private static readonly Vector3 _bayonetModeItemRotationOffset = new(180f, 20f, 220f);

    private Vector3 _currentItemPositionOffset;
    private Vector3 _currentItemRotationOffset;

    private const int bulletHitId = 8832676;
    private const int bayonetHitId = 8832677;
    private const int bulletHitBufferCapacity = 10;
    private const int bayonetHitBufferCapacity = 25;

    public const float TIME_BETWEEN_FIRING_AND_BULLET_EXIT = 0.09f;
    private float _bulletRadius;
    private float _bulletMaxDistance;

    public NetworkVariable<int> currentAmmo { get; private set; } = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<bool> _isSafetyOn = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private int _bulletHitMask;
    private int _bayonetHitMask;
    private int _maxAmmo;

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

        _piercingRoundsEnabled = true;
        _bulletRadius = 0.1f;
        _bulletMaxDistance = 200f;
        _maxAmmo = 1;

        _currentItemPositionOffset = _gunModeItemPositionOffset;
        _currentItemRotationOffset = _gunModeItemRotationOffset;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _currentAttackMode.OnValueChanged += OnAttackModeChanged;
    }

    public override void OnNetworkDespawn()
    {
        _currentAttackMode.OnValueChanged -= OnAttackModeChanged;
        base.OnNetworkDespawn();
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

        TryRegisterImperiumInsights();
    }

    private void TryRegisterImperiumInsights()
    {
        // if (ImperiumIntegration.IsLoaded && !_hasRegisteredImperiumInsights)
        // {
        //     Imperium.API.Visualization.InsightsFor<Musket>()
        //         .UnregisterInsight("Used Up")
        //         .UnregisterInsight("Cooldown")
        //         .UnregisterInsight("Location")
        //
        //         .RegisterInsight("Ammo", item => item.currentAmmo.Value.ToString())
        //         .RegisterInsight("Trigger Safety", _ => _isSafetyOn.Value ? "On" : "Off")
        //         .RegisterInsight("Attack Mode", item => item._currentAttackMode == AttackMode.Gun
        //             ? "Shoot"
        //             : "Stab");
        //
        //     _hasRegisteredImperiumInsights = true;
        // }
    }

    public override void LateUpdate()
    {
        itemProperties.positionOffset = _isHeldByWaxSoldier ? Vector3.zero : _currentItemPositionOffset;
        itemProperties.rotationOffset = _isHeldByWaxSoldier ? Vector3.zero : _currentItemRotationOffset;

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

        switch (CurrentAttackMode)
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

    #region Bullet Logic
    private const int PLAYER_BULLET_DAMAGE = 150;
    private const int IHITTABLE_BULLET_DAMAGE = 10;

    public void SetupShotAndFire()
    {
        if (_shootingCoroutine != null) StopCoroutine(_shootingCoroutine);
        _shootingCoroutine = null;
        _isPerformingAttackAction = true;
        _shootingCoroutine = StartCoroutine(Shoot());
    }

    private IEnumerator Shoot()
    {
        // LogVerbose($"In {nameof(Shoot)}.");
        _isPerformingAttackAction = true;

        if (IsServer) PlayBulletParticleEffectAndAudioClientRpc();
        else PlayBulletParticleEffectAndAudioServerRpc();

        yield return new WaitForSeconds(TIME_BETWEEN_FIRING_AND_BULLET_EXIT); // The actual gunshot happens 0.09 seconds into the shoot audio clip

        PerformBulletLogic();
        _isPerformingAttackAction = false;
    }

    private void PerformBulletLogic()
    {
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value - 1, 0, _maxAmmo);
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        Ray bulletRay;
        if (isHeldByPlayer)
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

        // Nothing was hit by the bullet
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
                    DamagePlayerServerRpc(playerClientId, PLAYER_BULLET_DAMAGE, CauseOfDeath.Gunshots);
                    _playerDamageCooldowns.Start(playerClientId, 0.2f);

                    LogVerbose($"Musket bullet dealt {PLAYER_BULLET_DAMAGE} damage to {player.playerUsername}.");
                    hitResult = HitResult.Target;
                }
            }
            else if (hitTransform.TryGetComponent(out IHittable iHittable))
            {
                iHittable.Hit(IHITTABLE_BULLET_DAMAGE, bulletRay.origin, playerHeldBy, true, bulletHitId);
                LogVerbose($"Musket bullet dealt {IHITTABLE_BULLET_DAMAGE} damage to {iHittable}.");
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
            DrawBulletDebug(bulletRay, successfulHitIndicies);
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
    #endregion

    #region Player Held Bayonet Logic
    private const int PLAYER_BAYONET_DAMAGE = 20;
    private const int IHITTABLE_BAYONET_DAMAGE = 2;

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
        bool hitSomething = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = _bayonetHitBuffer[i];

            if (collider.CompareTag("Player") && collider.transform.TryGetComponent(out PlayerControllerB player))
            {
                LogVerbose($"isHeldByPlayer && playerHeldBy == player  => {isHeldByPlayer && playerHeldBy == player}, isHeldByPlayer = {isHeldByPlayer}, playerHeldBy = {playerHeldBy}");
                if (isHeldByPlayer && playerHeldBy == player) continue;

                ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(player);
                if (_playerDamageCooldowns.IsOnCooldown(playerClientId)) continue;

                DamagePlayerServerRpc(playerClientId, PLAYER_BAYONET_DAMAGE, CauseOfDeath.Stabbing);
                _playerDamageCooldowns.Start(playerClientId, 0.2f);
                hitSomething = true;
                continue;
            }

            if (collider.transform.TryGetComponent(out IHittable iHittable))
            {
                iHittable.Hit(IHITTABLE_BAYONET_DAMAGE, bayonetTip.forward, playerHeldBy, true, bayonetHitId);
                hitSomething = true;
            }
        }

        if (hitSomething && playerHeldBy)
            playerHeldBy.playerBodyAnimator.SetTrigger(ShovelHitAnimatorHash);
    }

    private void ApplyHoldPose(bool enable)
    {
        PlayerControllerB player = playerHeldBy ?? previousPlayerHeldBy;
        if (!player) return;

        Animator anim = player.playerBodyAnimator;

        // Clear/restore the musket's own grab anim (whatever is set in itemProperties.grabAnim,
        // e.g. "HoldShotgun"). Guard because grabAnim can be empty (i think)
        if (!string.IsNullOrEmpty(itemProperties.grabAnim))
            anim.SetBool(itemProperties.grabAnim, !enable);

        anim.SetBool(HoldLungAnimatorHash, enable);
    }
    #endregion

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

    #region RPCs and Networking Stuff
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

    [ServerRpc]
    private void PlayBulletParticleEffectAndAudioServerRpc()
    {
        PlayBulletParticleEffectAndAudioClientRpc();
    }

    [ClientRpc]
    private void PlayBulletParticleEffectAndAudioClientRpc()
    {
        PlayAudioClipType(nameof(shootSfx), nameof(shootAudioSource), 0,
            interrupt: true, audibleInWalkieTalkie: true, audibleByEnemies: !isHeldByEnemy);
        bulletParticles.Play(withChildren: true);
    }

    [ClientRpc]
    internal void TransformIntoMeshClientRpc()
    {
        ScanNodeProperties scanNode = GetComponentInChildren<ScanNodeProperties>();
        if (scanNode) Destroy(scanNode.gameObject);

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource) Destroy(audioSource); // PhysicsProps require an audio source on the same gameobject as this Musket component

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider) Destroy(boxCollider);

        Destroy(muzzleTip.gameObject); // Destroys all of the bullet particle effects
        Destroy(bulletRayOrigin.gameObject);
        Destroy(bayonetAttackPhysics.gameObject);
        Destroy(bayonetTip.gameObject);
        Destroy(shootAudioSource.gameObject);
        Destroy(otherAudioSource.gameObject);

        Destroy(this);
    }

    private void OnAttackModeChanged(int previous, int current)
    {
        AttackMode newAttackMode = (AttackMode)current;

        _currentItemPositionOffset = newAttackMode == AttackMode.Gun
            ? _gunModeItemPositionOffset
            : _bayonetModeItemPositionOffset;

        _currentItemRotationOffset = newAttackMode == AttackMode.Gun
            ? _gunModeItemRotationOffset
            : _bayonetModeItemRotationOffset;

        if (isHeldByPlayer) ApplyHoldPose(newAttackMode == AttackMode.Bayonet);
    }
    #endregion

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
        return $"[Musket {BioId}]";
    }

    #region Abstract Item Class Event Functions
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        _isHoldingButton = buttonDown;

        if (!playerHeldBy) return;

        bool canAttack = CanAttack(out AttackFailureReason failureReason);
        switch (CurrentAttackMode)
        {
            case AttackMode.Gun:
            {
                if (canAttack)
                {
                    SetupShotAndFire();
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
            _currentAttackMode.Value = (int)(CurrentAttackMode == AttackMode.Gun ? AttackMode.Bayonet : AttackMode.Gun);
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
        ApplyHoldPose(CurrentAttackMode == AttackMode.Bayonet);
    }

    public override void PocketItem()
    {
        ApplyHoldPose(false);
        base.PocketItem();
    }

    public override void DiscardItem()
    {
        playerHeldBy.equippedUsableItemQE = false;
        ApplyHoldPose(false);
        base.DiscardItem();
    }
    #endregion
}