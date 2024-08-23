using System;
using System.Collections.Generic;
using System.Text;
using Unity;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.RubberDuck
{
    public class RubberDuckItem  : PhysicsProp
    {
        public Material[] Materials;
        private NetworkVariable<int> RubberDuckVariable = new NetworkVariable<int>(0,default,default);
        void Awake()
        {
            RubberDuckVariable.Value = UnityEngine.Random.Range(0, Materials.Length);
            GetComponent<MeshRenderer>().materials[0] = Materials[RubberDuckVariable.Value];
        }
    }
}
