using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarItem : BiodiverseItem
    {
        public Light screenLight;

        public AudioSource mainObjectAudio;
        public AudioClip powerOnSound;
        public AudioClip powerOffSound;
        public AudioClip outOfPowerSound;
        public AudioClip beepingSound;

        private readonly Vector3 inspectingPosition = new(0.11f, -0.17f, 0.37f);
        private readonly Vector3 inspectingRotation = new(0, 109, 17);
        private readonly Color screenActiveColor = new(0.019f, 0.39f, 0f, 1f);


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

        public override void ItemActivate(bool used, bool buttonDown = true)
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
            if (!IsOwner || playerHeldBy == null || !playerHeldBy.IsInspectingItem)
            {
                base.LateUpdate();
                return;
            }
            if (parentObject != null)
            {
                transform.rotation = parentObject.rotation;
                transform.Rotate(inspectingRotation);
                transform.position = parentObject.position;
                Vector3 positionOffset = inspectingPosition;
                positionOffset = parentObject.rotation * positionOffset;
                transform.position += positionOffset;
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
