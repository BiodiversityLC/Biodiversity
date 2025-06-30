using Biodiversity.Util;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.Weapons;

public class Musket : BiodiverseItem
{
    [SerializeField] private Transform muzzleTip;

    private readonly NetworkVariable<int> currentAmmo = new(1);
    private int maxAmmo;

    private readonly NetworkVariable<bool> safetyOn = new();
    private bool heldByPlayer;

    private float bulletRadius;
    private float maxBulletDistance;

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();
        return currentAmmo.Value;
    }

    public override void LoadItemSaveData(int saveData)
    {
        base.LoadItemSaveData(saveData);
        currentAmmo.Value = saveData;
    }

    private void Shoot()
    {
        if (!IsOwner) return;
        
        // todo: add particle effects, anims and audio BEFORE the raycasting logic is done
        
        currentAmmo.Value = Mathf.Max(0, currentAmmo.Value - 1);
        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;

        Ray bulletRay = heldByPlayer
            ? new Ray(
                localPlayer.gameplayCamera.transform.position - localPlayer.gameplayCamera.transform.up * 0.45f,
                localPlayer.gameplayCamera.transform.forward)
            : new Ray(
                muzzleTip.position,
                muzzleTip.forward);
        
        // Detect and damage players
        RaycastHit[] playerColliders = new RaycastHit[10];
        int playersCaught = Physics.SphereCastNonAlloc(bulletRay, bulletRadius, playerColliders, maxBulletDistance,
            StartOfRound.Instance.collidersRoomMaskDefaultAndPlayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < playersCaught; i++)
        {
            RaycastHit playerCollider = playerColliders[i];
            PlayerControllerB player = playerCollider.transform.GetComponent<PlayerControllerB>();
            if (PlayerUtil.IsPlayerDead(player)) continue; // IsPlayerDead() (for now) does the null check for us

            float bulletTravelDistance = Vector3.Distance(bulletRay.origin, playerCollider.point);
            int bulletDamage = CalculateBulletDamage(bulletTravelDistance);
            DamagePlayerServerRpc(player.actualClientId, bulletDamage);
        }
    }

    private int CalculateBulletDamage(float bulletTravelDistance)
    {
        return 1;
    }

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
}