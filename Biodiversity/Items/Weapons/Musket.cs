using Biodiversity.Util;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.Weapons;

public class Musket : BiodiverseItem
{
    [SerializeField] private Transform muzzleTip;

    private enum DamageType
    {
        Player,
        IHittable
    }

    private RaycastHit[] hitBuffer;
    private Comparer<RaycastHit> raycastHitDistanceComparer;

    private EnemyAI enemyHeldBy;
    
    private float bulletRadius;
    private float maxBulletDistance;

    private readonly NetworkVariable<int> currentAmmo = new(1);
    private const int bulletHitId = 8832676;
    private int bulletMask;
    private int maxAmmo;

    private readonly NetworkVariable<bool> isSafetyOn = new();
    private const bool canHitEnemies = true;
    private bool isHeldByPlayer;
    
    private void Awake()
    {
        hitBuffer = new RaycastHit[2];
        bulletMask = StartOfRound.Instance.collidersRoomMaskDefaultAndPlayers | (1 << LayerMask.NameToLayer("Enemies"));
        raycastHitDistanceComparer = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
    }

    private bool CanShoot()
    {
        return (isHeld || isHeldByPlayer || isHeldByEnemy) && currentAmmo.Value > 0 && !isSafetyOn.Value;
    }

    private void Shoot()
    {
        if (!IsOwner) return;
        
        // todo: add particle effects, anims and audio BEFORE the raycasting logic below 
        
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value - 1, 0, maxAmmo);
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        Ray bulletRay = isHeldByPlayer
            ? new Ray(
                localPlayer.gameplayCamera.transform.position - localPlayer.gameplayCamera.transform.up * 0.45f,
                localPlayer.gameplayCamera.transform.forward)
            : new Ray(
                muzzleTip.position,
                muzzleTip.forward);

        int hitCount = Physics.SphereCastNonAlloc(bulletRay, bulletRadius, hitBuffer, maxBulletDistance, bulletMask,
            QueryTriggerInteraction.Collide);

        if (hitCount == 0) return;
        if (hitCount > 1)
            Array.Sort(hitBuffer, 0, hitCount, raycastHitDistanceComparer);

        // We have two colliders in the buffer to make sure that we don't accidently only capture the collider of the shooter
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            if (hit.transform.TryGetComponent(out PlayerControllerB player))
            {
                if (isHeldByPlayer && playerHeldBy == player) continue;
                
                int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.Player);
                DamagePlayerServerRpc(player.actualClientId, bulletDamage); // RPC is needed because `PlayerControllerB.DamagePlayer` has an `if (!IsOwner) return;` statement at the start
                break;
            }
            
            if (hit.transform.TryGetComponent(out IHittable iHittable))
            {
                if (hit.transform.TryGetComponent(out EnemyAICollisionDetect enemyAICollisionDetect) 
                    && (!canHitEnemies || (isHeldByEnemy && enemyHeldBy == enemyAICollisionDetect.mainScript))) continue;

                int bulletDamage = CalculateNormalizedBulletDamage(hit.distance, DamageType.IHittable);
                iHittable.Hit(bulletDamage, bulletRay.origin, playerHeldBy, true, bulletHitId);
                break;
            }
        }
    }

    private void Reload()
    {
        currentAmmo.Value = Mathf.Clamp(currentAmmo.Value + 1, 0, maxAmmo);
    }

    private int CalculateNormalizedBulletDamage(float bulletTravelDistance, DamageType damageType)
    {
        return 1;
    }

    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    private void DamagePlayerServerRpc(ulong playerId, int damage, Vector3 force = default)
    {
        DamagePlayerClientRpc(playerId, damage, force);
    }

    [ClientRpc]
    private void DamagePlayerClientRpc(ulong playerId, int damage, Vector3 force = default)
    {
        PlayerControllerB player = PlayerUtil.GetPlayerFromClientId(playerId);
        player.DamagePlayer(damage, true, true, CauseOfDeath.Gunshots, 0, false, force);
    }

    #endregion

    #region Abstract Item Class Event Functions

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        
        if (CanShoot()) Shoot();
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
        
        isHeldByPlayer = true;
        isHeldByEnemy = false;

        enemyHeldBy = null;
    }
    
    public override void PocketItem()
    {
        base.PocketItem();

        isHeldByPlayer = true;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }

    public override void GrabItem()
    {
        base.GrabItem();
        
        isHeldByPlayer = true;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }

    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
        
        isHeldByPlayer = false;
        isHeldByEnemy = true;
        enemyHeldBy = enemy;
        playerHeldBy = null;
    }
    
    public override void DiscardItem()
    {
        base.DiscardItem();

        isHeldByPlayer = false;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }

    public override void DiscardItemFromEnemy()
    {
        base.DiscardItemFromEnemy();
        
        isHeldByPlayer = false;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }

    #endregion
}