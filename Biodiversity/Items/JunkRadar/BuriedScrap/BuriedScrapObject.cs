using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class BuriedScrapObject : NetworkBehaviour
    {
        public DiggingState diggingState = DiggingState.IsBuried;
        public DiggingState nonActiveDiggingState = DiggingState.IsBuried;
        private Coroutine diggingCoroutine = null;
        private int numberOfDiggingInteractions = 0;
        private int numberOfDiggingWithShovels = 0;
        private bool isLocalPlayerDiggingWithShovel = false;
        private readonly float diggingSpeedIncreaseFactor = 0.6f;  // multiply digging speed by (1 + this factor)
        private float currentHudFillAmount = 0f;

        private bool isDisplayingDiggingTips = false;
        private readonly string diggingTip = "Hold with shovel in hands for faster digging";

        public GrabbableObject buriedItem;
        private BoxCollider buriedItemBoxCollider;
        private Vector3 itemBuriedPosition;
        private Vector3 itemHalfBuriedPosition;
        private Vector3 itemDuggedPosition;
        private JunkRadarItem masterJunkRadar;
        private bool isEnabled = false;

        private float valueLossTimer = 0f;
        private readonly float valueLossInterval = 1.5f;
        private readonly int loseValueSturdyChance = 0;
        private readonly int loseValueFragileChance = 20;
        private readonly int loseValueUltraFragileChance = 80;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;
        public AudioSource loseValueAudio;

        public ParticleSystem buriedEnemyParticles;


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

        public void Update()
        {
            if (!isEnabled && masterJunkRadar != null)
            {
                if (masterJunkRadar.hasBeenHeld)
                {
                    EnableBuriedScrap();
                }
            }
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (diggingState != DiggingState.NotBuried && diggingTrigger.enabled)
            {
                diggingTrigger.interactable = localPlayer != null && !localPlayer.isPlayerDead && (localPlayer.currentlyHeldObjectServer == null || localPlayer.currentlyHeldObjectServer is Shovel);
            }
            if (diggingTrigger.enabled && diggingTrigger.interactable && localPlayer != null && !localPlayer.isPlayerDead && localPlayer.hoveringOverTrigger != null && localPlayer.hoveringOverTrigger == diggingTrigger)
            {
                if (!isDisplayingDiggingTips)
                {
                    ManageDiggingControlTips(localPlayer, shouldDisplayTips: true);
                    isDisplayingDiggingTips = true;
                }
            }
            else
            {
                if (isDisplayingDiggingTips)
                {
                    ManageDiggingControlTips(localPlayer, shouldDisplayTips: false);
                    isDisplayingDiggingTips = false;
                }
            }
            if (diggingState == DiggingState.Digging && localPlayer != null && !localPlayer.isPlayerDead && localPlayer.isHoldingInteract && localPlayer.hoveringOverTrigger != null
                && localPlayer.hoveringOverTrigger == diggingTrigger && localPlayer.currentlyHeldObjectServer != null && localPlayer.currentlyHeldObjectServer is Shovel)
            {
                if (!isLocalPlayerDiggingWithShovel)
                {
                    ModifyDiggingSpeedServerRpc(increaseSpeed: true, usingShovel: true);
                    isLocalPlayerDiggingWithShovel = true;
                }
            }
            else
            {
                if (isLocalPlayerDiggingWithShovel)
                {
                    if (diggingTrigger.timeToHoldSpeedMultiplier != 1)
                        ModifyDiggingSpeedServerRpc(increaseSpeed: false, usingShovel: true);
                    isLocalPlayerDiggingWithShovel = false;
                }
            }
            if (diggingState == DiggingState.Digging && diggingTrigger.enabled && IsServer && numberOfDiggingWithShovels >= 1)
            {
                valueLossTimer += Time.deltaTime;
                if (valueLossTimer >= (valueLossInterval / numberOfDiggingWithShovels))
                {
                    valueLossTimer = 0f;
                    TryLoseValueServerRpc();
                }
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
                        ModifyDiggingSpeed(increaseSpeed: true);
                    }
                    break;
                case DiggingState.FinishDigging:
                case DiggingState.CancelDigging:
                    if (newDiggingState == DiggingState.CancelDigging)  // Still digging but with 1 less player
                    {
                        numberOfDiggingInteractions--;
                        if (numberOfDiggingInteractions != 0)
                        {
                            ModifyDiggingSpeed(increaseSpeed: false);
                            return;
                        }
                    }
                    // Stop digging completely
                    valueLossTimer = 0f;
                    currentHudFillAmount = 0f;
                    numberOfDiggingWithShovels = 0;
                    numberOfDiggingInteractions = 0;
                    diggingTrigger.timeToHoldSpeedMultiplier = 1f;
                    diggingParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    diggingAudio.Stop();
                    if (diggingCoroutine != null)
                        StopCoroutine(diggingCoroutine);
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
            valueLossTimer = 0f;
            numberOfDiggingInteractions = 1;
            diggingTrigger.timeToHoldSpeedMultiplier = 1f;
            diggingParticles.Play();
            diggingAudio.pitch = Random.Range(0.9f, 1.1f);
            diggingAudio.Play();
            var time = 0f;
            var startPosition = buriedItem.targetFloorPosition;
            var endPosition = nonActiveDiggingState == DiggingState.HalfBuried ? itemDuggedPosition : itemHalfBuriedPosition;
            while (time < diggingTrigger.timeToHold)
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / diggingTrigger.timeToHold);
                time += Time.deltaTime * diggingTrigger.timeToHoldSpeedMultiplier;
                buriedItem.targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                currentHudFillAmount += Time.deltaTime * diggingTrigger.timeToHoldSpeedMultiplier;
                yield return null;
            }
            buriedItem.targetFloorPosition = endPosition;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ModifyDiggingSpeedServerRpc(bool increaseSpeed, bool usingShovel = false)
        {
            ModifyDiggingSpeedClientRpc(increaseSpeed, usingShovel);
        }

        [ClientRpc]
        private void ModifyDiggingSpeedClientRpc(bool increaseSpeed, bool usingShovel)
        {
            ModifyDiggingSpeed(increaseSpeed, usingShovel);
        }

        private void ModifyDiggingSpeed(bool increaseSpeed, bool usingShovel = false)
        {
            if (increaseSpeed)
            {
                diggingTrigger.timeToHoldSpeedMultiplier += diggingSpeedIncreaseFactor;
                PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                if (localPlayer != null && !localPlayer.isPlayerDead && localPlayer.isHoldingInteract && localPlayer.hoveringOverTrigger != null && localPlayer.hoveringOverTrigger == diggingTrigger)
                {
                    HUDManager.Instance.holdFillAmount = currentHudFillAmount;
                }
            }
            else
            {
                if (diggingTrigger.timeToHoldSpeedMultiplier != 1)
                {
                    diggingTrigger.timeToHoldSpeedMultiplier -= diggingSpeedIncreaseFactor;
                    if (diggingTrigger.timeToHoldSpeedMultiplier == 1)
                    {
                        valueLossTimer = 0f;
                    }
                }
            }
            numberOfDiggingWithShovels += usingShovel ? (increaseSpeed ? 1 : -1) : 0;
        }

        private void ManageDiggingControlTips(PlayerControllerB localPlayer, bool shouldDisplayTips)
        {
            HUDManager.Instance.ClearControlTips();
            if (shouldDisplayTips)
            {
                bool usingShovel = localPlayer.currentlyHeldObjectServer != null && localPlayer.currentlyHeldObjectServer is Shovel;
                Item itemProperties = usingShovel ? localPlayer.currentlyHeldObjectServer.itemProperties : null;
                HUDManager.Instance.ChangeControlTipMultiple(usingShovel ? itemProperties.toolTips.Add(diggingTip) : [diggingTip], holdingItem: usingShovel, itemProperties);
            }
            else
            {
                if (localPlayer.currentlyHeldObjectServer != null)
                {
                    Item itemProperties = localPlayer.currentlyHeldObjectServer.itemProperties;
                    HUDManager.Instance.ChangeControlTipMultiple(itemProperties.toolTips, holdingItem: true, itemProperties);
                }
            }
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

        [ServerRpc]
        private void TryLoseValueServerRpc()
        {
            int chanceToLoseValue = BuriedScrapsList.AllItems[buriedItem.itemProperties.itemName].Status switch
            {
                BuriedScrapsList.BuriedScrapStatus.Fragile => loseValueFragileChance,
                BuriedScrapsList.BuriedScrapStatus.UltraFragile => loseValueUltraFragileChance,
                _ => loseValueSturdyChance
            };
            if (chanceToLoseValue > 0 && Random.Range(0, 100) < chanceToLoseValue)
                LoseValueClientRpc();
        }

        [ClientRpc]
        private void LoseValueClientRpc()
        {
            loseValueAudio.pitch = Random.Range(0.7f, 1.3f);
            loseValueAudio.Play();
            buriedItem.SetScrapValue((int)(buriedItem.scrapValue / 2.0f));
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
                    SyncItemServerRpc(itemComponent.NetworkObject, itemComponent.scrapValue, calculatedBuriedScrapRotY);
                }
            }
        }

        [ServerRpc]
        private void SyncItemServerRpc(NetworkObjectReference itemRef, int scrapValue, float calculatedBuriedScrapRotY)
        {
            SyncItemClientRpc(itemRef, scrapValue, calculatedBuriedScrapRotY);
        }

        [ClientRpc]
        private void SyncItemClientRpc(NetworkObjectReference itemRef, int scrapValue, float calculatedBuriedScrapRotY)
        {
            StartCoroutine(SyncItem(itemRef, scrapValue, calculatedBuriedScrapRotY));
        }

        private IEnumerator SyncItem(NetworkObjectReference itemRef, int scrapValue, float calculatedBuriedScrapRotY)
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
            buriedItem.hasHitGround = true;
            buriedItem.reachedFloorTarget = true;
            buriedItem.isInFactory = false;
            if (buriedItem.itemProperties.isScrap)
                buriedItem.SetScrapValue(scrapValue);
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
