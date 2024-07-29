using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Biodiversity.Util.Scripts
{
    internal class SplineObject : MonoBehaviour
    {
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform Handle;
        [SerializeField] private Transform endPoint;
        [SerializeField] private Transform meta1;
        [SerializeField] private Transform meta2;
        private Transform point;
        [NonSerialized] public float interpolateAmount = 0f;
        [SerializeField] private bool Debug = false;

        private bool forward = true;

        private void Start()
        {
            point = this.gameObject.transform;
        }

        private void Update()
        {
            if (Debug)
            {
                if (forward)
                {
                    forward = !UpdateForward(0.3f, Time.deltaTime);
                }
                else
                {
                    forward = UpdateBackward(0.3f, Time.deltaTime);
                }
            }
        }

        public bool UpdateForward(float speed, float delta)
        {
            interpolateAmount = interpolateAmount + delta * speed;

            meta1.position = Vector3.Lerp(startPoint.position, Handle.position, interpolateAmount);
            meta2.position = Vector3.Lerp(Handle.position, endPoint.position, interpolateAmount);

            point.position = Vector3.Lerp(meta1.position, meta2.position, interpolateAmount);

            if (interpolateAmount >= 1)
            {
                interpolateAmount = 1;
                return true;
            }
            return false;
        }

        public bool UpdateBackward(float speed, float delta)
        {
            interpolateAmount = interpolateAmount - delta * speed;

            meta1.position = Vector3.Lerp(startPoint.position, Handle.position, interpolateAmount);
            meta2.position = Vector3.Lerp(Handle.position, endPoint.position, interpolateAmount);

            point.position = Vector3.Lerp(meta1.position, meta2.position, interpolateAmount);

            if (interpolateAmount <= 0)
            {
                interpolateAmount = 0;
                return true;
            }
            return false;
        }
    }
}
