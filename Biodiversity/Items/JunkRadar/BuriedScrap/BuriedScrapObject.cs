using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class BuriedScrapObject : NetworkBehaviour
    {
        public DiggingState diggingState = DiggingState.IsBuried;
        public DiggingState nonActiveDiggingState = DiggingState.IsBuried;
        private Coroutine diggingCoroutine = null;
        private int numberOfDiggingInteractions = 0;
        private readonly float diggingSpeedIncreasePerInteraction = 0.4f;

        public GrabbableObject buriedItem;
        private BoxCollider buriedItemBoxCollider;
        private Vector3 itemBuriedPosition;
        private Vector3 itemHalfBuriedPosition;
        private Vector3 itemDuggedPosition;
        private JunkRadarItem masterJunkRadar;
        private bool isEnabled = false;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

        public ParticleSystem buriedEnemyParticles;

        public void Update()
        {
            if (!isEnabled && masterJunkRadar != null)
            {
                if (masterJunkRadar.hasBeenHeld)
                {
                    EnableBuriedScrap();
                }
            }
            if (diggingState != DiggingState.NotBuried && diggingTrigger.enabled)
            {
                var player = GameNetworkManager.Instance.localPlayerController;
                diggingTrigger.interactable = player != null && !player.isPlayerDead && player.currentlyHeldObjectServer == null;
            }
        }

        private void EnableBuriedScrap()
        {
            isEnabled = true;
            diggingTrigger.enabled = true;
            diggingCollider.enabled = true;
            masterJunkRadar.detectedBuriedScraps.Add(this);
        }

        public void StartDigging(PlayerControllerB player)
        {
            DiggingServerRpc(DiggingState.Digging);
        }

        public void FinishDigging(PlayerControllerB player)
        {
            if (player != null && !player.isPlayerDead)
                DiggingServerRpc(DiggingState.FinishDigging);
        }

        public void CancelDigging(PlayerControllerB player)
        {
            DiggingServerRpc(DiggingState.CancelDigging);
        }

        [ServerRpc(RequireOwnership = false)]
        private void DiggingServerRpc(DiggingState newDiggingState)
        {
            if (newDiggingState == DiggingState.FinishDigging)
            {
                if (diggingState == DiggingState.FinishDigging)
                    return;
                diggingState = newDiggingState;
            }
            DiggingClientRpc(newDiggingState);
        }

        [ClientRpc]
        private void DiggingClientRpc(DiggingState newDiggingState)
        {
            switch (newDiggingState)
            {
                case DiggingState.Digging:
                    if (diggingState != DiggingState.Digging)  // Start digging (singleplayer)
                    {
                        if (diggingCoroutine != null)
                            StopCoroutine(diggingCoroutine);
                        diggingCoroutine = StartCoroutine(DiggingAction());
                    }
                    else  // Speed up digging (digging multiplayer)
                    {
                        numberOfDiggingInteractions++;
                        diggingTrigger.timeToHoldSpeedMultiplier += numberOfDiggingInteractions * diggingSpeedIncreasePerInteraction;
                    }
                    break;
                case DiggingState.FinishDigging:
                case DiggingState.CancelDigging:
                    if (newDiggingState == DiggingState.CancelDigging)  // Still digging but with 1 less player
                    {
                        numberOfDiggingInteractions--;
                        if (numberOfDiggingInteractions != 0)
                        {
                            diggingTrigger.timeToHoldSpeedMultiplier -= numberOfDiggingInteractions * diggingSpeedIncreasePerInteraction;
                            return;
                        }
                    }
                    // Stop digging completely
                    diggingParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    diggingAudio.Stop();
                    if (diggingCoroutine != null)
                        StopCoroutine(diggingCoroutine);
                    if (newDiggingState == DiggingState.FinishDigging)
                    {
                        numberOfDiggingInteractions = 0;
                        diggingTrigger.timeToHoldSpeedMultiplier = 1f;
                    }
                    if (nonActiveDiggingState == DiggingState.HalfBuried)  // If half buried
                    {
                        if (newDiggingState == DiggingState.FinishDigging)  // If finish digging
                        {
                            diggingTrigger.enabled = false;
                            diggingCollider.enabled = false;
                            masterJunkRadar.detectedBuriedScraps.Remove(this);
                            if (TryManageBuriedEnemy())
                            {
                                break;
                            }
                            buriedItem.grabbable = true;
                            buriedItem.grabbableToEnemies = true;
                            if (buriedItemBoxCollider != null)
                            {
                                buriedItemBoxCollider.enabled = true;
                            }
                            buriedItem.targetFloorPosition = itemDuggedPosition;  // Then is now dugged
                        }
                        else
                        {
                            buriedItem.targetFloorPosition = itemHalfBuriedPosition;  // Else is back to half buried
                        }
                    }
                    else  // If fully buried
                    {
                        if (newDiggingState == DiggingState.FinishDigging)  // If finish digging
                        {
                            var diggingEmission = diggingParticles.emission;
                            diggingEmission.rateOverTime = 40;
                            buriedItem.targetFloorPosition = itemHalfBuriedPosition;  // Then is now half buried
                        }
                        else
                        {
                            buriedItem.targetFloorPosition = itemBuriedPosition;  // Else is back to fully buried
                        }
                    }
                    break;
                default:
                    break;
            }
            if (newDiggingState == DiggingState.FinishDigging)
            {
                nonActiveDiggingState = nonActiveDiggingState == DiggingState.IsBuried ? DiggingState.HalfBuried : DiggingState.FinishDigging;
            }
            diggingState = newDiggingState;
        }

        private IEnumerator DiggingAction()
        {
            numberOfDiggingInteractions = 1;
            diggingTrigger.timeToHoldSpeedMultiplier = 1f;
            diggingParticles.Play();
            diggingAudio.pitch = Random.Range(0.9f, 1.1f);
            diggingAudio.Play();
            var time = 0f;
            var startPosition = buriedItem.targetFloorPosition;
            var endPosition = nonActiveDiggingState == DiggingState.HalfBuried ? itemDuggedPosition : itemHalfBuriedPosition;
            while (time < (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier))
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier));
                time += Time.deltaTime;
                buriedItem.targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                yield return null;
            }
            buriedItem.targetFloorPosition = endPosition;
        }

        /// <summary>
        /// Despawn the buried item prefab and spawn the associated enemy prefab at the same position
        /// </summary>
        /// <returns>True if the enemy was spawned successfully and false if the buried item is not an enemy</returns>
        private bool TryManageBuriedEnemy()
        {
            var buriedScrapProperties = BuriedScrapsList.AllItems[buriedItem.itemProperties.itemName];
            if (buriedScrapProperties.Origin != BuriedScrapsList.BuriedScrapOrigin.BioEnemy)
            {
                return false;
            }
            buriedEnemyParticles.Play();
            if (IsServer)
            {
                GameObject enemyObject = Instantiate(buriedScrapProperties.enemyPrefab, buriedItem.transform.position, Quaternion.Euler(new Vector3(0f, buriedItem.transform.eulerAngles.y, 0f)));
                enemyObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                RoundManager.Instance.SpawnedEnemies.Add(enemyObject.GetComponent<EnemyAI>());
                buriedItem.NetworkObject.Despawn();
            }
            return true;
        }

        public override bool Equals(object other)
        {
            if (other == null || other is not BuriedScrapObject)
                return false;
            else
                return this.transform.position == ((BuriedScrapObject)other).transform.position;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        [ServerRpc]
        public void SyncMasterServerRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef, float calculatedBuriedScrapRotY)
        {
            SyncMasterClientRpc(buriedScrapRef, masterJunkRadarRef, calculatedBuriedScrapRotY);
        }

        [ClientRpc]
        private void SyncMasterClientRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef, float calculatedBuriedScrapRotY)
        {
            StartCoroutine(SyncMaster(buriedScrapRef, masterJunkRadarRef, calculatedBuriedScrapRotY));
        }

        private IEnumerator SyncMaster(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef, float calculatedBuriedScrapRotY)
        {
            NetworkObject itemNetObject = null;
            masterJunkRadarRef.TryGet(out var masterNetObject);
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 8f && !buriedScrapRef.TryGet(out itemNetObject))
            {
                yield return new WaitForSeconds(0.03f);
            }
            if (itemNetObject == null || masterNetObject == null)
            {
                yield break;
            }
            yield return new WaitForEndOfFrame();
            masterJunkRadar = masterNetObject.GetComponent<JunkRadarItem>();
            diggingTrigger.enabled = false;
            diggingCollider.enabled = false;
            SpawnItem(calculatedBuriedScrapRotY);
        }

        private void SpawnItem(float calculatedBuriedScrapRotY)
        {
            if (IsServer)
            {
                var item = BuriedScrapsList.GetRandomItem();
                if (item != null)
                {
                    var itemObject = Instantiate(item, transform.position, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
                    var itemComponent = itemObject.GetComponent<GrabbableObject>();
                    itemComponent.transform.rotation = Quaternion.Euler(itemComponent.itemProperties.restingRotation);
                    itemComponent.fallTime = 1f;
                    itemComponent.hasHitGround = true;
                    itemComponent.reachedFloorTarget = true;
                    itemComponent.isInFactory = false;
                    if (itemComponent.itemProperties.isScrap)
                        itemComponent.SetScrapValue((int)(Random.Range(itemComponent.itemProperties.minValue, itemComponent.itemProperties.maxValue) * RoundManager.Instance.scrapValueMultiplier));
                    itemComponent.NetworkObject.Spawn();
                    SyncItemServerRpc(itemComponent.NetworkObject, calculatedBuriedScrapRotY);
                }
            }
        }

        [ServerRpc]
        private void SyncItemServerRpc(NetworkObjectReference itemRef, float calculatedBuriedScrapRotY)
        {
            SyncItemClientRpc(itemRef, calculatedBuriedScrapRotY);
        }

        [ClientRpc]
        private void SyncItemClientRpc(NetworkObjectReference itemRef, float calculatedBuriedScrapRotY)
        {
            StartCoroutine(SyncItem(itemRef, calculatedBuriedScrapRotY));
        }

        private IEnumerator SyncItem(NetworkObjectReference itemRef, float calculatedBuriedScrapRotY)
        {
            NetworkObject itemNetObject = null;
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 8f && !itemRef.TryGet(out itemNetObject))
            {
                yield return new WaitForSeconds(0.03f);
            }
            if (itemNetObject == null)
            {
                yield break;
            }
            yield return new WaitForSeconds(1f);
            buriedItem = itemNetObject.GetComponent<GrabbableObject>();
            buriedItem.grabbable = false;
            buriedItem.grabbableToEnemies = false;
            buriedItemBoxCollider = buriedItem.GetComponent<BoxCollider>();
            if (buriedItemBoxCollider != null)
            {
                buriedItemBoxCollider.enabled = false;
            }
            if (buriedItem.radarIcon != null)
            {
                Destroy(buriedItem.radarIcon.gameObject);
            }
            if (buriedItem.insertedBattery != null && buriedItem.itemProperties.requiresBattery)
            {
                buriedItem.insertedBattery.charge = 0.5f;
            }
            var buriedScrapProperties = BuriedScrapsList.AllItems[buriedItem.itemProperties.itemName];
            itemBuriedPosition = buriedItem.targetFloorPosition + new Vector3(0f, buriedScrapProperties.UndergroundPosition.buried, 0f);
            itemHalfBuriedPosition = buriedItem.targetFloorPosition + new Vector3(0f, buriedScrapProperties.UndergroundPosition.halfBuried, 0f);
            itemDuggedPosition = buriedItem.targetFloorPosition + new Vector3(0f, buriedScrapProperties.UndergroundPosition.dugged, 0f);
            buriedItem.targetFloorPosition = itemBuriedPosition;
            buriedItem.transform.rotation = Mathf.Abs(buriedItem.transform.rotation.eulerAngles.x) < 90f ?
                Quaternion.Euler(buriedItem.transform.rotation.eulerAngles.x, calculatedBuriedScrapRotY, buriedItem.transform.rotation.eulerAngles.z + buriedScrapProperties.UndergroundRotation)
              : Quaternion.Euler(buriedItem.transform.rotation.eulerAngles.x + buriedScrapProperties.UndergroundRotation, calculatedBuriedScrapRotY, buriedItem.transform.rotation.eulerAngles.z);
        }
    }
}
