using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.Ogopogo
{
    internal class TerrainDetectOgo : MonoBehaviour
    {
        public OgopogoAI ogopogoAI;

        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.layer == 1 << 8 && ogopogoAI.currentBehaviourStateIndex == (int)OgopogoAI.State.Rising && ogopogoAI.transform.position.y - ogopogoAI.water.gameObject.transform.position.y > 20 && !ogopogoAI.playerHasBeenGrabbed)
            {
                ogopogoAI.TerrainDetect();
            }
        }
    }
}
