using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class MaskedMugItem : BiodiverseItem
    {
        public RandomPeriodicAudioPlayer randomAudioPlayer;

        public static SpawnableEnemyWithRarity enemyToSpawn;  // filled by MaskedMugPatches

        private bool isAudioEnabled = false;
        private readonly Vector3 inspectingPosition = new(-0.15f, 0.2f, 0f);

        public bool isEnemySpawning = false;
        private float enemySpawningTimer = 0f;
        private readonly float enemySpawningInterval = 3f;

        public override void Update()
        {
            base.Update();
            if (!isAudioEnabled && grabbable && isHeld && playerHeldBy != null)
            {
                isAudioEnabled = true;
                randomAudioPlayer.enabled = true;
            }
            if (isAudioEnabled && isEnemySpawning)
            {
                enemySpawningTimer += Time.deltaTime;
                if (enemySpawningTimer >= enemySpawningInterval)
                {
                    enemySpawningTimer = 0f;
                    isEnemySpawning = false;//SpawnMaskedEnemy(playerHeldBy.playerClientId, playerHeldBy.transform.position);
                }
            }
        }

        public override void LateUpdate()
        {
            if (playerHeldBy != null && IsOwner && playerHeldBy.IsInspectingItem)
            {
                if (parentObject != null)
                {
                    transform.rotation = parentObject.rotation;
                    transform.Rotate(itemProperties.rotationOffset);
                    transform.position = parentObject.position;
                    transform.position += (parentObject.rotation * inspectingPosition);
                }
                if (radarIcon != null)
                {
                    radarIcon.position = transform.position;
                }
                return;
            }
            base.LateUpdate();
        }

        // used by MaskedMugPatches
        [ServerRpc(RequireOwnership = false)]
        public void SpawnMaskedEnemyServerRpc(ulong playerId, Vector3 position)
        {
            StartCoroutine(SpawnMaskedEnemy(playerId, position));
        }

        private IEnumerator SpawnMaskedEnemy(ulong playerId, Vector3 position)
        {
            if (!IsServer || enemyToSpawn == null)
            {
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
            PlayerControllerB player = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
            bool isInFactory = position.y < -80f;
            Vector3 spawnPosition = RoundManager.Instance.GetNavMeshPosition(position, default, sampleRadius: 10f);
            if (!RoundManager.Instance.GotNavMeshPositionResult)
                spawnPosition = PositionUtils.GetClosestAINodePosition(isInFactory ? RoundManager.Instance.insideAINodes : RoundManager.Instance.outsideAINodes, position);
            NetworkObjectReference netObjectRef = RoundManager.Instance.SpawnEnemyGameObject(spawnPosition, Random.Range(-90f, 90f), -1, enemyToSpawn.enemyType);
            if (netObjectRef.TryGet(out NetworkObject networkObject))
            {
                MaskedPlayerEnemy maskedEnemy = networkObject.GetComponent<MaskedPlayerEnemy>();
                maskedEnemy.SetSuit(player.currentSuitID);
                maskedEnemy.mimickingPlayer = player;
                maskedEnemy.SetEnemyOutside(!isInFactory);
                maskedEnemy.CreateMimicClientRpc(netObjectRef, isInFactory, (int)playerId);
            }
        }
    }
}
