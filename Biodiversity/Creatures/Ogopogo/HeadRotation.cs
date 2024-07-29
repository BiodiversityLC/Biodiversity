using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Biodiversity.Creatures.Enemy
{
    public class HeadRotation : MonoBehaviour
    {
        public GameObject ThingToCopy;

        // Update is called once per frame
        void LateUpdate()
        {
            this.transform.rotation = ThingToCopy.transform.rotation * Quaternion.Euler(-74.022f, 0, 0);
        }
    }
}
