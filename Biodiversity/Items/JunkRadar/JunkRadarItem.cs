using Biodiversity.Items.JunkRadar.BuriedScrap;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static Biodiversity.Util.ExtensionMethods;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarItem : BiodiverseItem
    {
        [Space(5f)]
        public DiggingState diggingState = DiggingState.NotBuried;
        private Coroutine diggingCoroutine = null;
        private Vector3 buriedPosition;
        private Vector3 duggedPosition;
        private int numberOfDiggingInteractions = 0;
        private bool isLocalPlayerDiggingWithShovel = false;
        private readonly float diggingSpeedIncreaseFactor = 1f;  // multiply digging speed by (1 + this factor)
        private float currentHudFillAmount = 0f;
        private bool buriedScrapsInitialized = false;
        private int minBuriedScrapsAmount = 5;
        private int maxBuriedScrapsAmount = 7;

        public Light screenLight;
        public BoxCollider grabCollider;
        public ScanNodeProperties scanNodeProperties;
        private readonly Color screenActiveColor = new(0.01f, 0.3f, 0f, 1f);

        public AudioSource mainObjectAudio;
        public AudioSource detectAudio;
        public AudioClip powerOnSound;
        public AudioClip powerOffSound;
        public AudioClip outOfPowerSound;
        public AudioClip newDetectSound;
        public AudioClip beepingSound;
        public AudioClip detectBelowSound;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

        public GameObject screenSignalValid;
        public GameObject screenSignalInvalid;
        public Image screenItemImage;
        public Image[] screenArrowsImagesL;
        public Image[] screenArrowsImagesR;
        public MeshRenderer[] screenTextValidRenderers;
        public MeshRenderer[] screenTextInvalidRenderers;
        public Color baseImageColor;
        public Color sturdyItemImageColor;
        public Color fragileItemImageColor;
        public Color ultraFragileItemImageColor;

        public List<BuriedScrapObject> detectedBuriedScraps = [];
        private Vector3? previousClosestDetectedPosition = null;
        private bool isAboveDetectedScrap = false;
        private bool hasApproachedDetectedScrap = false;
        private int maxDetectedDistance = 50;
        private readonly float maxDetectedDistanceOffset = 0.15f;
        private float refreshTimer = 0f;
        private readonly float refreshInterval = 0.3f;
        private float beepingTimer = 0f;
        private float? beepingInterval = null;
        private readonly Vector2 beepingIntervalMaxMin = new(1.7f, 0.1f);
        private float? beepingPitch = null;
        private readonly Vector2 beepingPitchMinMax = new(0.9f, 1.3f);

        public bool isBeingCharged = false;
        private float rechargeTimer = 0;
        private readonly float maxRechargeTime = 2f;

        private readonly Vector3 inspectingPosition = new(0.11f, -0.17f, 0.37f);
        private readonly Vector3 inspectingRotation = new(0, 109, 17);
        private readonly Vector3 rechargingPosition = new(0, 0, 0.1f);
        private readonly Vector3 rechargingRotation = new(0, 0, -45);


        public static JunkRadarItem Instance { get; private set; }  // this is not a singleton, it's the Instance of the "master" item
        private bool isOriginalInstance = false;  // true if the actual item is the "master"


        protected override string GetLogPrefix()
        {
            return $"[JunkRadarItem {BioId} - Master({isOriginalInstance})]";
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResetAllImagesOnScreen();
            screenSignalValid.SetActive(false);
            maxDetectedDistance = JunkRadarHandler.Instance.Config.MaxDetectionDistance;
            string minMaxAmountConfig = JunkRadarHandler.Instance.Config.BuriedScrapsAmountMinMax;
            if (!string.IsNullOrEmpty(minMaxAmountConfig))
            {
                var valuesArray = minMaxAmountConfig.Split(',').Select(s => s.Trim()).ToArray();
                if (valuesArray.Length != 2)
                    return;
                if (!int.TryParse(valuesArray[0], out var minV) || !int.TryParse(valuesArray[1], out var maxV))
                    return;
                if (minV > maxV)
                    return;
                minBuriedScrapsAmount = minV;
                maxBuriedScrapsAmount = maxV;
            }
        }

        [ServerRpc]
        public void SetBuriedStateServerRpc(NetworkObjectReference itemRef, int randomRotationY)
        {
            SetBuriedStateClientRpc(itemRef, randomRotationY);
        }

        [ClientRpc]
        private void SetBuriedStateClientRpc(NetworkObjectReference itemRef, int randomRotationY)
        {
            StartCoroutine(SetBuriedState(itemRef, randomRotationY));
        }

        private IEnumerator SetBuriedState(NetworkObjectReference itemRef, int randomRotationY)
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
            if (!isHeld && !isHeldByEnemy)
            {
                LogInfo("Setting Junk Radar to buried state");
                if (Instance == null)
                {
                    Instance = this;
                    isOriginalInstance = true;
                    if (!buriedScrapsInitialized)
                    {
                        InitializeBuriedScraps();
                    }
                }
                diggingState = DiggingState.IsBuried;
                diggingTrigger.enabled = true;
                diggingCollider.enabled = true;
                var diggingEmission = diggingParticles.emission;
                diggingEmission.rateOverTime = 40;
                hasHitGround = true;
                reachedFloorTarget = true;
                isInFactory = false;
                grabbable = false;
                grabbableToEnemies = false;
                grabCollider.enabled = false;
                insertedBattery.charge = 0.5f;
                targetFloorPosition.y -= 0.0855f;
                buriedPosition = targetFloorPosition;
                duggedPosition = buriedPosition + new Vector3(0f, 0.12f, 0f);
                transform.rotation = Quaternion.Euler(0, randomRotationY, 15);
            }
        }

        internal void InitializeBuriedScraps()
        {
            LogInfo("Initializing Buried Scraps");
            buriedScrapsInitialized = true;
            detectedBuriedScraps.Clear();
            if (IsServer)
            {
                for (int i = 0; i < Random.Range(minBuriedScrapsAmount, maxBuriedScrapsAmount + 1); i++)
                {
                    var spawnPosition = !hasBeenHeld && i == 0 ? PositionUtils.GetRandomPositionNearPosition(transform.position, randomizePositionRadius: 15) : PositionUtils.GetRandomMoonPosition(randomizePositionRadius: 10);
                    var gameObject = Instantiate(JunkRadarHandler.Instance.Assets.BuriedScrapPrefab, spawnPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
                    gameObject.GetComponent<NetworkObject>().Spawn();
                    var buriedScrapObject = gameObject.GetComponent<BuriedScrapObject>();
                    buriedScrapObject.SyncMasterServerRpc(buriedScrapObject.NetworkObject, base.NetworkObject, Random.Range(0, 360));
                }
            }
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
                        ModifyDiggingSpeed(increaseSpeed: true);
                    }
                    break;
                case DiggingState.FinishDigging:
                case DiggingState.CancelDigging:
                    if (newDiggingState == DiggingState.CancelDigging)
                    {
                        numberOfDiggingInteractions--;
                        if (numberOfDiggingInteractions != 0)
                        {
                            ModifyDiggingSpeed(increaseSpeed: false);
                            return;
                        }
                    }
                    currentHudFillAmount = 0f;
                    numberOfDiggingInteractions = 0;
                    diggingTrigger.timeToHoldSpeedMultiplier = 1f;
                    diggingParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    diggingAudio.Stop();
                    if (diggingCoroutine != null)
                        StopCoroutine(diggingCoroutine);
                    if (newDiggingState == DiggingState.FinishDigging)
                    {
                        diggingTrigger.enabled = false;
                        diggingCollider.enabled = false;
                        grabbable = true;
                        grabbableToEnemies = true;
                        grabCollider.enabled = true;
                        scanNodeProperties.maxRange /= 2;
                        targetFloorPosition = duggedPosition;
                    }
                    else
                    {
                        targetFloorPosition = buriedPosition;
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
            var startPosition = targetFloorPosition;
            var endPosition = duggedPosition;
            while (time < diggingTrigger.timeToHold)
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / diggingTrigger.timeToHold);
                time += Time.deltaTime * diggingTrigger.timeToHoldSpeedMultiplier;
                targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                currentHudFillAmount += Time.deltaTime * diggingTrigger.timeToHoldSpeedMultiplier;
                //HUDManager.Instance.holdFillAmount += Time.deltaTime;
                yield return null;
            }
            targetFloorPosition = endPosition;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ModifyDiggingSpeedServerRpc(bool increaseSpeed)
        {
            ModifyDiggingSpeedClientRpc(increaseSpeed);
        }

        [ClientRpc]
        private void ModifyDiggingSpeedClientRpc(bool increaseSpeed)
        {
            ModifyDiggingSpeed(increaseSpeed);
        }

        private void ModifyDiggingSpeed(bool increaseSpeed)
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
                    diggingTrigger.timeToHoldSpeedMultiplier -= diggingSpeedIncreaseFactor;
            }
        }

        public override void Update()
        {
            base.Update();
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (diggingState != DiggingState.NotBuried && diggingTrigger.enabled)
            {
                diggingTrigger.interactable = localPlayer != null && !localPlayer.isPlayerDead && (localPlayer.currentlyHeldObjectServer == null || localPlayer.currentlyHeldObjectServer is Shovel);
            }
            if (diggingState == DiggingState.Digging && localPlayer != null && !localPlayer.isPlayerDead && localPlayer.isHoldingInteract && localPlayer.hoveringOverTrigger != null
                && localPlayer.hoveringOverTrigger == diggingTrigger && localPlayer.currentlyHeldObjectServer != null && localPlayer.currentlyHeldObjectServer is Shovel)
            {
                if (!isLocalPlayerDiggingWithShovel)
                {
                    ModifyDiggingSpeedServerRpc(increaseSpeed: true);
                    isLocalPlayerDiggingWithShovel = true;
                }
            }
            else
            {
                if (isLocalPlayerDiggingWithShovel)
                {
                    if (diggingTrigger.timeToHoldSpeedMultiplier != 1)
                        ModifyDiggingSpeedServerRpc(increaseSpeed: false);
                    isLocalPlayerDiggingWithShovel = false;
                }
            }
            if (isBeingUsed)
            {
                if ((screenSignalValid.activeSelf && (StartOfRound.Instance.inShipPhase || isInFactory))
                    || (screenSignalInvalid.activeSelf && !StartOfRound.Instance.inShipPhase && !isInFactory))
                {
                    ActivateScreenUI(true);
                }
                if (screenSignalValid.activeSelf && !isPocketed && Instance != null)
                {
                    refreshTimer += Time.deltaTime;
                    if (refreshTimer >= refreshInterval)
                    {
                        refreshTimer = 0f;
                        RefreshDetectedBuriedScraps();
                    }
                    if (beepingInterval.HasValue && beepingPitch.HasValue)
                    {
                        beepingTimer += Time.deltaTime;
                        if (beepingTimer >= beepingInterval.Value)
                        {
                            beepingTimer = 0f;
                            detectAudio.pitch = beepingPitch.Value;
                            detectAudio.PlayOneShot(isAboveDetectedScrap ? detectBelowSound : beepingSound);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Refresh the screen display with the closest detected buried scrap
        /// </summary>
        private void RefreshDetectedBuriedScraps()
        {
            var sortedScraps = Instance.detectedBuriedScraps.Where(s => s != null && s.IsSpawned && s.diggingTrigger.enabled).OrderBy(s => Vector3.Distance(transform.position, s.transform.position));

            if (sortedScraps.Count() == 0)
            {
                ResetAllImagesOnScreen();
            }
            else
            {
                // Calculate opacity based on distance to closest scrap
                BuriedScrapObject closestScrap = sortedScraps.First();
                Vector3 directionToBuriedScrap = closestScrap.transform.position - (isHeld && playerHeldBy != null ? playerHeldBy.transform.position : transform.position);
                float distanceToBuriedScrap = directionToBuriedScrap.magnitude;
                float normalizedDistance = Mathf.Clamp01(distanceToBuriedScrap / maxDetectedDistance);  // scraps above max distance will not be detected
                float opacity = 1 - normalizedDistance;
                float opacityOffseted = opacity - maxDetectedDistanceOffset;

                // Apply matching color based on opacity and detected scrap properties
                if (opacity >= 0)
                {
                    Color scrapColor = BuriedScrapsList.AllItems[closestScrap.buriedItem.itemProperties.itemName].Status switch
                    {
                        BuriedScrapsList.BuriedScrapStatus.Fragile => fragileItemImageColor,
                        BuriedScrapsList.BuriedScrapStatus.UltraFragile => ultraFragileItemImageColor,
                        _ => sturdyItemImageColor,
                    };
                    screenItemImage.color = ColorWithAlpha(scrapColor, opacityOffseted >= 0f ? opacityOffseted : 0f);

                    // Play a specific sfx if a new scrap has been detected
                    if ((previousClosestDetectedPosition == null || previousClosestDetectedPosition != closestScrap.transform.position) && opacityOffseted >= 0)
                    {
                        mainObjectAudio.PlayOneShot(newDetectSound);
                        previousClosestDetectedPosition = closestScrap.transform.position;
                    }
                }
                else
                {
                    ResetAllImagesOnScreen();
                    return;
                }

                if (opacityOffseted > 0f)
                {
                    // Manages arrows opacity based on direction angle
                    if (isHeld && playerHeldBy != null)
                    {
                        float angle = Vector3.SignedAngle(playerHeldBy.transform.forward, directionToBuriedScrap, Vector3.up);
                        if (angle > -1 && angle < 1)
                        {
                            screenArrowsImagesL[0].color = ColorWithAlpha(screenArrowsImagesL[0].color, opacityOffseted);
                            screenArrowsImagesR[0].color = ColorWithAlpha(screenArrowsImagesR[0].color, opacityOffseted);
                            for (int i = 1; i < screenArrowsImagesL.Length; i++)
                            {
                                screenArrowsImagesL[i].color = ColorWithAlpha(screenArrowsImagesL[i].color, 0f);
                                screenArrowsImagesR[i].color = ColorWithAlpha(screenArrowsImagesR[i].color, 0f);
                            }
                        }
                        else if (angle < 0)
                        {
                            screenArrowsImagesL[0].color = ColorWithAlpha(screenArrowsImagesL[0].color, opacityOffseted);
                            screenArrowsImagesL[1].color = ColorWithAlpha(screenArrowsImagesL[0].color, angle < -40 ? opacityOffseted : 0f);
                            screenArrowsImagesL[2].color = ColorWithAlpha(screenArrowsImagesL[0].color, angle < -90 ? opacityOffseted : 0f);
                            for (int i = 0; i < screenArrowsImagesR.Length; i++)
                            {
                                screenArrowsImagesR[i].color = ColorWithAlpha(screenArrowsImagesR[i].color, 0f);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < screenArrowsImagesL.Length; i++)
                            {
                                screenArrowsImagesL[i].color = ColorWithAlpha(screenArrowsImagesL[i].color, 0f);
                            }
                            screenArrowsImagesR[0].color = ColorWithAlpha(screenArrowsImagesR[0].color, opacityOffseted);
                            screenArrowsImagesR[1].color = ColorWithAlpha(screenArrowsImagesR[0].color, angle > 40 ? opacityOffseted : 0);
                            screenArrowsImagesR[2].color = ColorWithAlpha(screenArrowsImagesR[0].color, angle > 90 ? opacityOffseted : 0);
                        }
                    }
                    // Manages screen beeping interval and pitch based on distance
                    if (isAboveDetectedScrap = opacityOffseted >= (1 - maxDetectedDistanceOffset - 0.02f))
                    {
                        if (!hasApproachedDetectedScrap)
                        {
                            beepingTimer = 1f;
                            hasApproachedDetectedScrap = true;
                        }
                        beepingInterval = 1f;
                        beepingPitch = 1f;
                    }
                    else
                    {
                        // linear interpolation of beeping interval based on image opacity
                        beepingInterval = beepingIntervalMaxMin.x + (opacityOffseted * ((beepingIntervalMaxMin.y - beepingIntervalMaxMin.x) / (1 - maxDetectedDistanceOffset)));
                        // linear interpolation of beeping pitch based on beeping interval
                        beepingPitch = beepingPitchMinMax.x + ((beepingPitchMinMax.y - beepingPitchMinMax.x) * ((beepingInterval - beepingIntervalMaxMin.x) / (beepingIntervalMaxMin.y - beepingIntervalMaxMin.x)));
                        hasApproachedDetectedScrap = false;
                    }
                }
                else
                {
                    // Reset arrows because the detected scrap is too far away
                    Color resetColor = ColorWithAlpha(baseImageColor, 0f);
                    for (int i = 0; i < screenArrowsImagesL.Length; i++)
                    {
                        screenArrowsImagesL[i].color = resetColor;
                    }
                    for (int i = 0; i < screenArrowsImagesR.Length; i++)
                    {
                        screenArrowsImagesR[i].color = resetColor;
                    }
                    beepingInterval = null;
                    beepingPitch = null;
                }
            }
        }


        private void ResetAllImagesOnScreen()
        {
            screenItemImage.color = ColorWithAlpha(baseImageColor, 0f);
            for (int i = 0; i < screenArrowsImagesL.Length; i++)
            {
                screenArrowsImagesL[i].color = screenItemImage.color;
            }
            for (int i = 0; i < screenArrowsImagesR.Length; i++)
            {
                screenArrowsImagesR[i].color = screenItemImage.color;
            }
            previousClosestDetectedPosition = null;
            beepingInterval = null;
            beepingPitch = null;
        }

        private void ActivateRadar(bool activate)
        {
            isBeingUsed = activate;
            screenLight.enabled = activate;
            mainObjectRenderer.material.SetColor("_EmissiveColor", activate ? screenActiveColor : Color.black);
            mainObjectAudio.PlayOneShot(activate ? powerOnSound : powerOffSound, 0.9f);
            SetControlTipsForItem();
            ActivateScreenUI(activate);
        }

        private void ActivateScreenUI(bool activate)
        {
            if (activate)
            {
                bool validSignal = !StartOfRound.Instance.inShipPhase && !isInFactory;
                screenSignalValid.SetActive(validSignal);
                screenSignalInvalid.SetActive(!validSignal);
            }
            else
            {
                screenSignalValid.SetActive(false);
                screenSignalInvalid.SetActive(false);
                detectAudio.Stop(stopOneShots: true);
            }
            if (!activate || screenSignalInvalid.activeSelf)
            {
                refreshTimer = 0f;
                beepingTimer = 0f;
                beepingInterval = null;
                beepingPitch = null;
            }
            if (screenSignalValid.activeSelf)
            {
                foreach (var renderer in screenTextValidRenderers)
                {
                    renderer.enabled = true;
                }
            }
            if (screenSignalInvalid.activeSelf)
            {
                foreach (var renderer in screenTextInvalidRenderers)
                {
                    renderer.enabled = true;
                }
            }
        }

        public override void ItemActivate(bool used, bool buttonDown = true)  // synced
        {
            base.ItemActivate(used, buttonDown);
            if (playerHeldBy == null || insertedBattery == null || insertedBattery.empty)
                return;
            ActivateRadar(!isBeingUsed);
        }

        public override void EquipItem()
        {
            base.EquipItem();
            if (isBeingUsed)
            {
                screenLight.enabled = true;
                ActivateScreenUI(true);
            }
        }

        public override void PocketItem()
        {
            base.PocketItem();
            if (isBeingUsed)
            {
                screenLight.enabled = false;
                ActivateScreenUI(false);
            }
        }

        public override void DiscardItem()
        {
            base.DiscardItem();
        }

        public override void UseUpBatteries()
        {
            base.UseUpBatteries();
            ActivateRadar(false);
            mainObjectAudio.PlayOneShot(outOfPowerSound, 0.8f);
        }

        public override void SetControlTipsForItem()
        {
            if (IsOwner)
                HUDManager.Instance.ChangeControlTipMultiple([(isBeingUsed ? "Turn off" : "Activate") + " : [RMB]", "Inspect: [Z]"], holdingItem: true, itemProperties);
        }

        public override void LateUpdate()
        {
            if (playerHeldBy != null)
            {
                if (isBeingCharged)
                {
                    UpdatePositionSpecific(rechargingPosition, rechargingRotation);
                    rechargeTimer += Time.deltaTime;
                    if (rechargeTimer >= maxRechargeTime)
                    {
                        isBeingCharged = false;
                        rechargeTimer = 0;
                    }
                    return;
                }
                else if (IsOwner && playerHeldBy.IsInspectingItem)
                {
                    UpdatePositionSpecific(inspectingPosition, inspectingRotation);
                    return;
                }
            }
            base.LateUpdate();
        }

        private void UpdatePositionSpecific(Vector3 specificPosition, Vector3 specificRotation)
        {
            if (parentObject != null)
            {
                transform.rotation = parentObject.rotation;
                transform.Rotate(specificRotation);
                transform.position = parentObject.position;
                transform.position += (parentObject.rotation * specificPosition);
            }
            if (radarIcon != null)
            {
                radarIcon.position = transform.position;
            }
        }

        public override int GetItemDataToSave()
        {
            return buriedScrapsInitialized ? 1 : 0;
        }

        public override void LoadItemSaveData(int saveData)
        {
            buriedScrapsInitialized = saveData == 1;
            if (buriedScrapsInitialized)
            {
                hasBeenHeld = true;
                Instance = this;
                isOriginalInstance = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (isOriginalInstance)
            {
                Instance = null;
            }
            base.OnNetworkDespawn();
        }
    }
}
