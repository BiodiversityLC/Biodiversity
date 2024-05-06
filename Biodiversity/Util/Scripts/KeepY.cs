using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Biodiversity.Util.Scripts
{
    internal class KeepY : MonoBehaviour
    {
        public float? StartingY = null;

        public void Init()
        {
            StartingY = this.transform.position.y;
        }

        public void LateUpdate() {
            if (StartingY != null)
            {
                this.transform.position = new Vector3(this.transform.position.x, StartingY.Value, this.transform.position.z);
            }
        }
    }
}
