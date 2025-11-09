using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

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
        private bool buriedScrapsInitialized = false;

        public Light screenLight;
        private readonly Color screenActiveColor = new(0.01f, 0.3f, 0f, 1f);

        public AudioSource mainObjectAudio;
        public AudioClip powerOnSound;
        public AudioClip powerOffSound;
        public AudioClip outOfPowerSound;
        public AudioClip beepingSound;

        public InteractTrigger diggingTrigger;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

        public bool isBeingCharged = false;
        private float rechargeTimer = 0;
        private readonly float maxRechargeTime = 2f;

        private readonly Vector3 inspectingPosition = new(0.11f, -0.17f, 0.37f);
        private readonly Vector3 inspectingRotation = new(0, 109, 17);
        private readonly Vector3 rechargingPosition = new(0, 0, 0.1f);
        private readonly Vector3 rechargingRotation = new(0, 0, -45);

        public static JunkRadarItem Instance { get; private set; }  // this is not a singleton, it's the Instance of the "master" item
        private bool isOriginalInstance = false;  // true if the actual item is the "master"


        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance == null && !StartOfRound.Instance.inShipPhase)
            {
                Instance = this;
                isOriginalInstance = true;
                if (!buriedScrapsInitialized)
                {
                    InitializeBuriedScraps();
                    StartCoroutine(SetBuriedState());
                }
            }
        }

        private IEnumerator SetBuriedState()
        {
            yield return new WaitForSeconds(1f);
            if (!isHeld && !isHeldByEnemy)
            {
                LogInfo("Setting Junk Radar to buried state");
                diggingState = DiggingState.IsBuried;
                diggingTrigger.enabled = true;
                grabbable = false;
                grabbableToEnemies = false;
                insertedBattery.charge = 0.5f;
                targetFloorPosition.y -= 0.0855f;
                buriedPosition = targetFloorPosition;
                duggedPosition = buriedPosition + new Vector3(0f, 0.1f, 0f);
                transform.rotation = Quaternion.Euler(0, new System.Random(StartOfRound.Instance.randomMapSeed).Next(0, 360), 15);
            }
        }

        public void StartDigging(PlayerControllerB player)
        {
            if (player != null && !player.isPlayerDead)
                DiggingServerRpc(DiggingState.Digging);
        }

        public void FinishDigging(PlayerControllerB player)
        {
            if (player != null && !player.isPlayerDead)
                DiggingServerRpc(DiggingState.FinishDigging);
        }

        public void CancelDigging(PlayerControllerB player)
        {
            if (player != null && !player.isPlayerDead)
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
                        // to be implemented: increase digging speed with each interaction
                    }
                    break;
                case DiggingState.FinishDigging:
                case DiggingState.CancelDigging:
                    diggingParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmitting);
                    diggingAudio.Stop();
                    if (diggingCoroutine != null)
                        StopCoroutine(diggingCoroutine);
                    if (newDiggingState == DiggingState.FinishDigging)
                    {
                        diggingTrigger.enabled = false;
                        grabbable = true;
                        grabbableToEnemies = true;
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
            diggingParticles.Play();
            diggingAudio.pitch = Random.Range(0.9f, 1.1f);
            diggingAudio.Play();
            var time = 0f;
            var startPosition = targetFloorPosition;
            var endPosition = startPosition + new Vector3(0f, 0.1f, 0f);
            while (time < (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier))
            {
                float lerpFactor = Mathf.SmoothStep(0f, 1f, time / (diggingTrigger.timeToHold / diggingTrigger.timeToHoldSpeedMultiplier));
                time += Time.deltaTime;
                targetFloorPosition = Vector3.Lerp(startPosition, endPosition, lerpFactor);
                yield return null;
            }
            targetFloorPosition = endPosition;
        }

        internal void InitializeBuriedScraps()
        {
            LogInfo("Initializing Buried Scraps");
            buriedScrapsInitialized = true;
        }

        internal void EnabledBuriedScraps()
        {
            LogInfo("Enabling Buried Scraps");
        }

        public override void Update()
        {
            base.Update();
            if (diggingState != DiggingState.NotBuried && diggingTrigger.enabled)
            {
                var player = GameNetworkManager.Instance.localPlayerController;
                diggingTrigger.interactable = player != null && !player.isPlayerDead && player.currentlyHeldObjectServer == null;
            }
        }

        private void ActivateRadar(bool activate)
        {
            isBeingUsed = activate;
            screenLight.enabled = activate;
            mainObjectRenderer.material.SetColor("_EmissiveColor", activate ? screenActiveColor : Color.black);
            mainObjectAudio.PlayOneShot(activate ? powerOnSound : powerOffSound);
            SetControlTipsForItem();
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
            if (!hasBeenHeld && isOriginalInstance && buriedScrapsInitialized)
                EnabledBuriedScraps();  // this will only run once for this instance
            base.EquipItem();
            if (isBeingUsed)
                screenLight.enabled = true;
        }

        public override void PocketItem()
        {
            base.PocketItem();
            if (isBeingUsed)
                screenLight.enabled = false;
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

        protected override string GetLogPrefix()
        {
            return $"[JunkRadarItem {BioId} - Master({isOriginalInstance})]";
        }
    }
}
