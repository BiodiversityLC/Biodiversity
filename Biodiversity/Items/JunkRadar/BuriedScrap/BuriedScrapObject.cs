using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class BuriedScrapObject : NetworkBehaviour
    {
        public DiggingState diggingState = DiggingState.IsBuried;
        private Coroutine diggingCoroutine = null;
        private int numberOfDiggingInteractions = 0;
        private readonly float diggingSpeedIncreasePerInteraction = 0.4f;

        private GrabbableObject buriedItem;
        private BoxCollider buriedItemBoxCollider;
        private Vector3 itemBuriedPosition;
        private Vector3 itemDuggedPosition;
        private JunkRadarItem masterJunkRadar;
        private bool isEnabled = false;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

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
            /*var diggingEmission = diggingParticles.emission;
            diggingEmission.rateOverTime = 40;*/
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
                    if (diggingState != DiggingState.Digging)
                    {
                        if (diggingCoroutine != null)
                            StopCoroutine(diggingCoroutine);
                        diggingCoroutine = StartCoroutine(DiggingAction());
                    }
                    else
                    {
                        numberOfDiggingInteractions++;
                        diggingTrigger.timeToHoldSpeedMultiplier += numberOfDiggingInteractions * diggingSpeedIncreasePerInteraction;
                    }
                    break;
                case DiggingState.FinishDigging:
                case DiggingState.CancelDigging:
                    if (newDiggingState == DiggingState.CancelDigging)
                    {
                        numberOfDiggingInteractions--;
                        if (numberOfDiggingInteractions != 0)
                        {
                            diggingTrigger.timeToHoldSpeedMultiplier -= numberOfDiggingInteractions * diggingSpeedIncreasePerInteraction;
                            return;
                        }
                    }
                    diggingParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    diggingAudio.Stop();
                    if (diggingCoroutine != null)
                        StopCoroutine(diggingCoroutine);
                    if (newDiggingState == DiggingState.FinishDigging)
                    {
                        numberOfDiggingInteractions = 0;
                        diggingTrigger.timeToHoldSpeedMultiplier = 1f;
                        diggingTrigger.enabled = false;
                        diggingCollider.enabled = false;
                        buriedItem.grabbable = true;
                        buriedItem.grabbableToEnemies = true;
                        if (buriedItemBoxCollider != null)
                        {
                            buriedItemBoxCollider.enabled = true;
                        }
                        buriedItem.targetFloorPosition = itemDuggedPosition;
                    }
                    else
                    {
                        buriedItem.targetFloorPosition = itemBuriedPosition;
                    }
                    break;
                default:
                    break;
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
            var endPosition = itemDuggedPosition;
            while (time < (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier))
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier));
                time += Time.deltaTime;
                buriedItem.targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                yield return null;
            }
            buriedItem.targetFloorPosition = endPosition;
        }

        [ServerRpc]
        public void SyncMasterServerRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
        {
            SyncMasterClientRpc(buriedScrapRef, masterJunkRadarRef);
        }

        [ClientRpc]
        private void SyncMasterClientRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
        {
            StartCoroutine(SyncMaster(buriedScrapRef, masterJunkRadarRef));
        }

        private IEnumerator SyncMaster(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
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
            SpawnItem();
        }

        private void SpawnItem()
        {
            if (IsServer)
            {
                var itemsKeys = BuriedScrapsList.AllItems.Keys.ToList();
                var item = StartOfRound.Instance.allItemsList.itemsList.FirstOrDefault(i => i.itemName.Equals(itemsKeys[Random.Range(0, itemsKeys.Count)]));
                var itemObject = Instantiate(item.spawnPrefab, transform.position, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
                var itemComponent = itemObject.GetComponent<GrabbableObject>();
                itemComponent.fallTime = 1f;
                itemComponent.hasHitGround = true;
                itemComponent.reachedFloorTarget = true;
                itemComponent.isInFactory = false;
                itemComponent.NetworkObject.Spawn();
                SyncItemServerRpc(itemComponent.NetworkObject);
            }
        }

        [ServerRpc]
        private void SyncItemServerRpc(NetworkObjectReference itemRef)
        {
            SyncItemClientRpc(itemRef);
        }

        [ClientRpc]
        private void SyncItemClientRpc(NetworkObjectReference itemRef)
        {
            StartCoroutine(SyncItem(itemRef));
        }

        private IEnumerator SyncItem(NetworkObjectReference itemRef)
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
            yield return new WaitForEndOfFrame();
            buriedItem = itemNetObject.GetComponent<GrabbableObject>();
            buriedItem.grabbable = false;
            buriedItem.grabbableToEnemies = false;
            buriedItemBoxCollider = buriedItem.GetComponent<BoxCollider>();
            if (buriedItemBoxCollider != null)
            {
                buriedItemBoxCollider.enabled = false;
            }
            buriedItem.targetFloorPosition.y -= BuriedScrapsList.AllItems[buriedItem.itemProperties.itemName];
            itemBuriedPosition = buriedItem.targetFloorPosition;
            itemDuggedPosition = itemBuriedPosition + new Vector3(0f, 0.1f, 0f);
        }
    }
}
