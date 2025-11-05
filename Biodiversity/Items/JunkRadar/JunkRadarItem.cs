using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarItem : BiodiverseItem
    {
        public Light screenLight;
        private readonly Color screenActiveColor = new(0.01f, 0.3f, 0f, 1f);

        public AudioSource mainObjectAudio;
        public AudioClip powerOnSound;
        public AudioClip powerOffSound;
        public AudioClip outOfPowerSound;
        public AudioClip beepingSound;

        public bool isBeingCharged = false;
        public float rechargeTimer = 0;
        private readonly float maxRechargeTime = 2f;

        private readonly Vector3 inspectingPosition = new(0.11f, -0.17f, 0.37f);
        private readonly Vector3 inspectingRotation = new(0, 109, 17);
        private readonly Vector3 rechargingPosition = new(0, 0, 0.1f);
        private readonly Vector3 rechargingRotation = new(0, 0, -45);


        private void InitializeBuriedScraps()
        {

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
            if (!hasBeenHeld)
                InitializeBuriedScraps();
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

        protected override string GetLogPrefix()
        {
            return $"[JunkRadarItem {BioId}]";
        }
    }
}
