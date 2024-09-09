using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo
{
    public class HeadRotation : MonoBehaviour
    {
        public GameObject ThingToCopy;

        // Update is called once per frame
        private void LateUpdate()
        {
            transform.rotation = ThingToCopy.transform.rotation * Quaternion.Euler(-74.022f, 0, 0);
        }
    }
}
