using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.RubberDuck
{
    public class RubberDuckItem  : PhysicsProp
    {
        public MeshRenderer m;
        public Material[] Materials;
        private AudioSource Source;
        private Animator animator;
        void Awake()
        {

            var scripttexture = GetComponent<SetRandomTextureClass>();
            if (scripttexture == null)
                scripttexture = gameObject.AddComponent<SetRandomTextureClass>();
            scripttexture.Materials = Materials;
            animator = GetComponent<Animator>();
            Source = GetComponent<AudioSource>();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            animator.Play("Squeeze",-1,0);
            Source.Play();
            foreach (MouthDogAI mouthDog in FindObjectsOfType<MouthDogAI>())
            {
                mouthDog.DetectNoise(gameObject.transform.position, .5f);
            }
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
