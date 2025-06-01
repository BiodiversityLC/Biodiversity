using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilShell : PhysicsProp
    {
        public Vector3 Poffset;
        public override void LateUpdate()
        {
            itemProperties.positionOffset = heldByPlayerOnServer ? Poffset : Vector3.zero;
            base.LateUpdate();
        }
    }
}
