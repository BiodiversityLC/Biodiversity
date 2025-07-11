using Biodiversity.Items;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System;
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
    [SerializeField] private Transform muzzleTip;
    [SerializeField] private Transform bulletRayOrigin;
    
    [SerializeField] public MusketBayonetCollisionDetection bayonetCollisionDetection;
    #endregion
   

    private enum DamageType
    {
        Player,
        IHittable
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

    private RaycastHit[] bulletHitBuffer;
    private Collider[] bayonetHitBuffer;
    private Comparer<RaycastHit> raycastHitDistanceComparer;

    private CachedValue<Vector3> bayonetColliderHalfExtents;

    private Coroutine reelingUpCoroutine;
    private Coroutine shootingCoroutine;

    private AttackMode currentAttackMode;

    private Vector3 itemPositionOffset;
    private Vector3 itemRotationOffset;
    
    private float bulletRadius;
    private float maxBulletDistance;
    
    private const int bulletHitId = 8832676;
    private const int bayonetHitId = 8832677;
    private const int bulletHitBufferCapacity = 35;
    private const int bayonetHitBufferCapacity = 35;

    private readonly NetworkVariable<int> currentAmmo = new(1);
    private int bulletHitMask;
    private int bayonetHitMask;
    private int maxAmmo;

    private readonly NetworkVariable<bool> isSafetyOn = new();
    private bool isPerformingAttackAction;
    private bool isHoldingButton;
    private bool isHeldByWaxSoldier;
    
    private void Awake()
    {
        bulletHitBuffer = new RaycastHit[bulletHitBufferCapacity];
        bayonetHitBuffer = new Collider[bayonetHitBufferCapacity];
        
        bulletHitMask = StartOfRound.Instance.collidersRoomMaskDefaultAndPlayers | (1 << LayerMask.NameToLayer("Enemies"));
        bayonetHitMask = 1084754248; // See Util.VanillaLayersUtil for more details
        raycastHitDistanceComparer = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
        
        bulletRadius = 0.25f;
        maxBulletDistance = 200f;
        maxAmmo = 1;

        currentAttackMode = AttackMode.Gun;
        isSafetyOn.Value = false;
    }

    public override void Start()
    {
        base.Start();
        
        itemPositionOffset = itemProperties.positionOffset;
        itemRotationOffset = itemProperties.rotationOffset;

        bayonetColliderHalfExtents = new CachedValue<Vector3>(() => bayonetCollider.size * 0.5f);
    }

    private bool CanAttack(out AttackFailureReason failureReason)
    {
        failureReason = AttackFailureReason.None;
        if (!isHeld || !isHeldByPlayer && !isHeldByEnemy)
        {
            failureReason = AttackFailureReason.NotHeld;
            return false;
        }

        if (isPerformingAttackAction)
        {
            failureReason = AttackFailureReason.AlreadyPerformingAttackAction;
            return false;
        }

        if (currentAttackMode == AttackMode.Bayonet) return true;

        if (!isHoldingButton) //  `&& currentAttackMode == AttackMode.Gun` is implied
        {
            return false;
        }

        if (currentAmmo.Value <= 0)
        {
            failureReason = AttackFailureReason.NeedsReloading;
            return false;
        }
        
        if (isSafetyOn.Value)
        {
            failureReason = AttackFailureReason.SafetyIsOn;
            return false;
        }

        return true;
    }

    private IEnumerator Shoot()
    {
        LogVerbose($"In {nameof(Shoot)}");
        isPerformingAttackAction = true;
        
        PlayRandomAudioClipTypeServerRpc(nameof(shootSfx), nameof(shootAudioSource), true, audibleByEnemies: true);
        yield return new WaitForSeconds(0.09f); // The actual gunshot happens 0.09 seconds into the shoot audio clip
        PerformBulletLogic();
        isPerformingAttackAction = false;
    }

    private void PerformBulletLogic()
    {
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value - 1, 0, maxAmmo);
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        Ray bulletRay = isHeldByPlayer
            ? new Ray(
                localPlayer.gameplayCamera.transform.position - localPlayer.gameplayCamera.transform.up * 0.45f,
                localPlayer.gameplayCamera.transform.forward)
            : new Ray(
                bulletRayOrigin.position,
                bulletRayOrigin.forward);

        int hitCount = Physics.SphereCastNonAlloc(bulletRay, bulletRadius, bulletHitBuffer, maxBulletDistance, bulletHitMask,
            QueryTriggerInteraction.Collide);

        if (hitCount == 0) return;
        if (hitCount > 1)
            Array.Sort(bulletHitBuffer, 0, hitCount, raycastHitDistanceComparer);

        // We have >1 colliders in the buffer to make sure that we don't accidently only capture the collider of the shooter
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = bulletHitBuffer[i];

            if (hit.transform.TryGetComponent(out PlayerControllerB player))
            {
                if (isHeldByPlayer && playerHeldBy == player) continue;
                
                int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.Player);
                DamagePlayerServerRpc(player.actualClientId, bulletDamage, CauseOfDeath.Gunshots); // RPC is needed because `PlayerControllerB.DamagePlayer` has an `if (!IsOwner) return;` statement at the start
                LogVerbose($"Musket bullet delt {bulletDamage} damage to {player.playerUsername}.");
                break;
            }
            
            if (hit.transform.TryGetComponent(out IHittable iHittable))
            {
                if (hit.transform.TryGetComponent(out EnemyAICollisionDetect enemyAICollisionDetect)
                    && isHeldByEnemy && enemyHeldBy == enemyAICollisionDetect.mainScript) continue;

                int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.IHittable);
                iHittable.Hit(bulletDamage, bulletRay.origin, playerHeldBy, true, bulletHitId);
                LogVerbose($"Musket bullet delt {bulletDamage} damage to {iHittable}.");
                break;
            }
        }
    }

    private void Reload()
    {
        LogVerbose("Reloading...");
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value + 1, 0, maxAmmo);
    }

    private int CalculateNormalizedBulletDamage(float bulletTravelDistance, DamageType damageType)
    {
        return 100;
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
        yield return new WaitUntil(() => !isHoldingButton || !isHeld);
        
        playerUsingBayonet.playerBodyAnimator.SetBool(ReelingUpAnimatorHash, false);
        if (!isHeld)
        {
            LogVerbose("Musket has been dropped, cancelling bayonet stab.");
            isPerformingAttackAction = false;
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
            isPerformingAttackAction = false;
            yield break;
        }
        
        playerUsingBayonet.activatingItem = false;
        playerUsingBayonet.twoHanded = false;
        HitBayonet();
        
        yield return new WaitForSeconds(0.3f);
        isPerformingAttackAction = false;
    }

    public void HitBayonet()
    {
        LogVerbose($"In {nameof(HitBayonet)}");
        
        int hitCount = Physics.OverlapBoxNonAlloc(bayonetCollider.transform.position, bayonetColliderHalfExtents.Value,
            bayonetHitBuffer, Quaternion.identity, bayonetHitMask, QueryTriggerInteraction.Collide);
        
        if (hitCount == 0) return;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = bayonetHitBuffer[i];

            if (collider.CompareTag("Player") && collider.transform.TryGetComponent(out PlayerControllerB player))
            {
                if (isHeldByPlayer && playerHeldBy == player) continue;
                DamagePlayerServerRpc(player.actualClientId, 100, CauseOfDeath.Stabbing);
                continue;
            }

            if (collider.transform.TryGetComponent(out IHittable iHittable))
            {
                iHittable.Hit(4, bayonetTip.forward, playerHeldBy, true, bayonetHitId);
            }
        }
    }

    internal void OnGrabbedByWaxSoldier(EnemyAI waxSoldier)
    {
        isHeldByWaxSoldier = true;
        bayonetCollider.isTrigger = true;
        GrabItemFromEnemy(waxSoldier);
    }

    internal void OnDroppedByWaxSoldier()
    {
        isHeldByWaxSoldier = false;
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

    protected override string GetLogPrefix()
    {
        base.GetLogPrefix();
        return $"[Musket {BioId}]";
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

    #region Abstract Item Class Event Functions
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        isHoldingButton = buttonDown;
        
        if (!playerHeldBy) return;

        bool canAttack = CanAttack(out AttackFailureReason failureReason);
        switch (currentAttackMode)
        {
            case AttackMode.Gun:
            {
                if (canAttack)
                {
                    if (shootingCoroutine != null) StopCoroutine(shootingCoroutine);
                    shootingCoroutine = null;
                    isPerformingAttackAction = true;
                    shootingCoroutine = StartCoroutine(Shoot());
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
                    if (reelingUpCoroutine != null) StopCoroutine(reelingUpCoroutine);
                    reelingUpCoroutine = null;
                    isPerformingAttackAction = true;
                    reelingUpCoroutine = StartCoroutine(ReelUpBayonet());
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
        if (isPerformingAttackAction) return;
        if (right)
        {
            currentAttackMode = currentAttackMode == AttackMode.Gun ? AttackMode.Bayonet : AttackMode.Gun;
            LogVerbose($"Changed attack mode to {currentAttackMode}.");
        }
        else
        {
            string clipToPlay = isSafetyOn.Value ? nameof(turnSafetyOffSfx) : nameof(turnSafetyOnSfx);
            isSafetyOn.Value = !isSafetyOn.Value;
            PlayRandomAudioClipTypeServerRpc(clipToPlay, nameof(otherAudioSource), true, true, true);
        }
        
    }

    public override void LateUpdate()
    {
        itemProperties.positionOffset = isHeldByWaxSoldier ? Vector3.zero : itemPositionOffset;
        itemProperties.rotationOffset = isHeldByWaxSoldier ? Vector3.zero : itemRotationOffset;
        base.LateUpdate();
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