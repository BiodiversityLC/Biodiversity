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
        private readonly float diggingSpeedIncreasePerInteraction = 0.4f;
        private float currentHudFillAmount = 0f;
        private bool buriedScrapsInitialized = false;

        public Light screenLight;
        public BoxCollider grabCollider;
        public ScanNodeProperties scanNodeProperties;
        private readonly Color screenActiveColor = new(0.01f, 0.3f, 0f, 1f);

        public AudioSource mainObjectAudio;
        public AudioClip powerOnSound;
        public AudioClip powerOffSound;
        public AudioClip outOfPowerSound;
        public AudioClip beepingSound;
        public AudioClip newDetectSound;
        public AudioClip detectBelowSound;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

        public GameObject screenSignalValid;
        public GameObject screenSignalInvalid;
        public Image screenItemImage;
        public Image screenArrowLImage;
        public Image screenArrowRImage;
        public MeshRenderer[] screenTextValidRenderers;
        public MeshRenderer[] screenTextInvalidRenderers;
        public Color baseImageColor;
        public Color sturdyItemImageColor;
        public Color fragileItemImageColor;
        public Color ultraFragileItemImageColor;

        public List<BuriedScrapObject> detectedBuriedScraps = [];
        private Vector3? previousClosestDetectedPosition = null;
        private bool isAboveDetectedScrap = false;
        private readonly int maxDetectedDistance = 45;
        private readonly float maxDetectedDistanceOffset = 0.15f;
        private float refreshTimer = 0f;
        private readonly float refreshInterval = 0.5f;
        private float beepingTimer = 0f;
        private float? beepingInterval = null;
        private readonly Vector2 beepingIntervalMaxMin = new Vector2(2f, 0.1f);

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
                for (int i = 0; i < 6; i++)
                {
                    var spawnPosition = !hasBeenHeld && i == 0 ? PositionUtils.GetRandomPositionNearPosition(transform.position, randomizePositionRadius: 10) : PositionUtils.GetRandomMoonPosition(randomizePositionRadius: 15);
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
                        diggingTrigger.timeToHoldSpeedMultiplier += numberOfDiggingInteractions * diggingSpeedIncreasePerInteraction;
                        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                        if (localPlayer != null && !localPlayer.isPlayerDead && localPlayer.isHoldingInteract && localPlayer.hoveringOverTrigger != null && localPlayer.hoveringOverTrigger == diggingTrigger)
                        {
                            HUDManager.Instance.holdFillAmount += currentHudFillAmount;
                        }
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
                    currentHudFillAmount = 0f;
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
            while (time < (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier))
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier));
                time += Time.deltaTime;
                targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                currentHudFillAmount += Time.deltaTime * diggingTrigger.timeToHoldSpeedMultiplier;
                yield return null;
            }
            targetFloorPosition = endPosition;
        }

        public override void Update()
        {
            base.Update();
            if (diggingState != DiggingState.NotBuried && diggingTrigger.enabled)
            {
                var player = GameNetworkManager.Instance.localPlayerController;
                diggingTrigger.interactable = player != null && !player.isPlayerDead && player.currentlyHeldObjectServer == null;
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
                    if (beepingInterval != null)
                    {
                        beepingTimer += Time.deltaTime;
                        if (beepingTimer >= beepingInterval)
                        {
                            beepingTimer = 0f;
                            mainObjectAudio.PlayOneShot(beepingSound);
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
                BuriedScrapObject closestScrap = sortedScraps.First();
                Vector3 directionToBuriedScrap = closestScrap.transform.position - transform.position;
                float distanceToBuriedScrap = directionToBuriedScrap.magnitude;
                float normalizedDistance = Mathf.Clamp01(distanceToBuriedScrap / maxDetectedDistance);  // scraps above max distance will not be detected
                float opacity = 1 - normalizedDistance;
                float opacityOffseted = opacity - maxDetectedDistanceOffset;

                if (opacity >= 0)
                {
                    Color scrapColor = BuriedScrapsList.AllItems[closestScrap.buriedItem.itemProperties.itemName].Status switch
                    {
                        BuriedScrapsList.BuriedScrapStatus.Fragile => fragileItemImageColor,
                        BuriedScrapsList.BuriedScrapStatus.UltraFragile => ultraFragileItemImageColor,
                        _ => sturdyItemImageColor,
                    };
                    screenItemImage.color = ColorWithAlpha(scrapColor, opacityOffseted >= 0f ? opacityOffseted : 0f);

                    if (previousClosestDetectedPosition == null || previousClosestDetectedPosition != closestScrap.transform.position)
                    {
                        mainObjectAudio.PlayOneShot(newDetectSound);
                        previousClosestDetectedPosition = closestScrap.transform.position;
                        isAboveDetectedScrap = false;
                    }
                }
                else
                {
                    ResetAllImagesOnScreen();
                    return;
                }

                if (isHeld && playerHeldBy != null && opacityOffseted > 0f)
                {
                    if (Vector3.SignedAngle(playerHeldBy.transform.forward, directionToBuriedScrap, Vector3.up) < 0)
                    {
                        screenArrowLImage.color = ColorWithAlpha(screenArrowLImage.color, opacityOffseted);
                        screenArrowRImage.color = ColorWithAlpha(screenArrowRImage.color, 0f);
                    }
                    else
                    {
                        screenArrowLImage.color = ColorWithAlpha(screenArrowLImage.color, 0f);
                        screenArrowRImage.color = ColorWithAlpha(screenArrowRImage.color, opacityOffseted);
                    }
                    if (!isAboveDetectedScrap && opacityOffseted >= (1 - maxDetectedDistanceOffset - 0.05f))
                    {
                        mainObjectAudio.PlayOneShot(detectBelowSound);
                        isAboveDetectedScrap = true;
                    }
                    // linear interpolation of beeping interval based on image opacity
                    beepingInterval = beepingIntervalMaxMin.x + (opacityOffseted * ((beepingIntervalMaxMin.y - beepingIntervalMaxMin.x) / (1 - maxDetectedDistanceOffset)));
                }
                else
                {
                    screenArrowLImage.color = ColorWithAlpha(baseImageColor, 0f);
                    screenArrowRImage.color = screenArrowLImage.color;
                    beepingInterval = null;
                }
            }
        }


        private void ResetAllImagesOnScreen()
        {
            screenItemImage.color = ColorWithAlpha(baseImageColor, 0f);
            screenArrowLImage.color = screenItemImage.color;
            screenArrowRImage.color = screenItemImage.color;
            previousClosestDetectedPosition = null;
            isAboveDetectedScrap = false;
            beepingInterval = null;
        }

        private void ActivateRadar(bool activate)
        {
            isBeingUsed = activate;
            screenLight.enabled = activate;
            mainObjectRenderer.material.SetColor("_EmissiveColor", activate ? screenActiveColor : Color.black);
            mainObjectAudio.PlayOneShot(activate ? powerOnSound : powerOffSound);
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
            }
            if (!activate || screenSignalInvalid.activeSelf)
            {
                refreshTimer = 0f;
                beepingTimer = 0f;
                beepingInterval = null;
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
            mainObjectAudio.PlayOneShot(outOfPowerSound);
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
