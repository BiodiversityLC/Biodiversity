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

        public bool lastPlayerIsInFactoryOnDeath;
        public float lastPlayerRotationYOnDeath;

        private bool isAudioEnabled = false;
        private readonly Vector3 inspectingPosition = new(-0.15f, 0.2f, 0f);

        public override void Update()
        {
            base.Update();
            if (!isAudioEnabled && grabbable && isHeld && playerHeldBy != null)
            {
                isAudioEnabled = true;
                randomAudioPlayer.enabled = true;
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
        [ServerRpc]
        public void SpawnMaskedEnemyServerRpc(ulong playerId, Vector3 position)
        {
            if (enemyToSpawn == null)
            {
                return;
            }
            bool isInFactory = lastPlayerIsInFactoryOnDeath;
            Vector3 spawnPosition = RoundManager.Instance.GetNavMeshPosition(position, default, sampleRadius: 10f);
            if (!RoundManager.Instance.GotNavMeshPositionResult)
                spawnPosition = PositionUtils.GetClosestAINodePosition(isInFactory ? RoundManager.Instance.insideAINodes : RoundManager.Instance.outsideAINodes, position);
            NetworkObjectReference netObjectRef = RoundManager.Instance.SpawnEnemyGameObject(spawnPosition, lastPlayerRotationYOnDeath, -1, enemyToSpawn.enemyType);
            SyncMaskedEnemyClientRpc(netObjectRef, playerId, isInFactory);
        }

        [ClientRpc]
        private void SyncMaskedEnemyClientRpc(NetworkObjectReference netObjectRef, ulong playerId, bool isInFactory)
        {
            StartCoroutine(SyncMaskedEnemy(netObjectRef, playerId, isInFactory));
        }

        private IEnumerator SyncMaskedEnemy(NetworkObjectReference netObjectRef, ulong playerId, bool isInFactory)
        {
            var playerToMimic = StartOfRound.Instance.allPlayerObjects[playerId].GetComponent<PlayerControllerB>();
            NetworkObject netObject = null;
            float startTime = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => Time.realtimeSinceStartup - startTime > 20f || netObjectRef.TryGet(out netObject));
            if (playerToMimic.deadBody == null)
            {
                startTime = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => Time.realtimeSinceStartup - startTime > 20f || playerToMimic.deadBody != null);
            }
            if (playerToMimic.deadBody != null)
            {
                playerToMimic.deadBody.DeactivateBody(setActive: false);
            }
            if (netObject != null)
            {
                MaskedPlayerEnemy maskedEnemy = netObject.GetComponent<MaskedPlayerEnemy>();
                maskedEnemy.mimickingPlayer = playerToMimic;
                maskedEnemy.SetSuit(playerToMimic.currentSuitID);
                maskedEnemy.SetEnemyOutside(!isInFactory);
                maskedEnemy.SetVisibilityOfMaskedEnemy();
                playerToMimic.redirectToEnemy = maskedEnemy;
                maskedEnemy.enemyHP *= 2;  // :)
            }
        }
    }
}
