using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.Ogopogo
{
    internal class TerrainDetectOgo : MonoBehaviour
    {
        public OgopogoAI ogopogoAI;
        public Rigidbody rb;

        public void FixedUpdate()
        {
            rb.MovePosition(gameObject.transform.parent.position);
            rb.MoveRotation(gameObject.transform.parent.rotation);
        }

        public void OnCollisionEnter(Collision other)
        {
            BiodiversityPlugin.LogVerbose("Ogo hit a something.");
            ogopogoAI.TerrainDetect();
        }
    }
}
