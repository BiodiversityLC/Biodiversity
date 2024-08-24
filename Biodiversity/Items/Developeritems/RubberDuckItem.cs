using System;
using System.Collections.Generic;
using System.Text;
using Unity;
using Unity.Netcode;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;

namespace Biodiversity.Items.RubberDuck
{
    public class RubberDuckItem  : PhysicsProp
    {
        public MeshRenderer m;
        public Material[] Materials;
        void Awake()
        {
            var scripttexture = gameObject.AddComponent<SetRandomTextureClass>();
            scripttexture.Materials = Materials;
        }
    }

    public class SetRandomTextureClass : NetworkBehaviour
    {
        private NetworkVariable<int> RubberDuckVariable = new NetworkVariable<int>(0, default, default);
        public Material[] Materials;
        MeshRenderer Mesh;
        void Awake()
        {
            Mesh = GetComponent<MeshRenderer>();
        }
        void Start()
        {
            RubberDuckVariable.Value = UnityEngine.Random.Range(0, Materials.Length);
        }
        void Update()
        {
            Mesh.materials[0] = Materials[RubberDuckVariable.Value];
            Mesh.material = Materials[RubberDuckVariable.Value];
        }
       
    }
}
