using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarItem : BiodiverseItem
    {
        private readonly Vector3 inspectingPosition = new(0.11f, -0.17f, 0.37f);
        private readonly Vector3 inspectingRotation = new(0, 109, 17);


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
