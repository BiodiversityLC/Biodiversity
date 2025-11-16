using Biodiversity.Util.DataStructures;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class BuriedScrapObject : NetworkBehaviour
    {
        public DiggingState diggingState = DiggingState.IsBuried;
        private Coroutine diggingCoroutine = null;
        private Vector3 itemBuriedPosition;
        private Vector3 itemDuggedPosition;
        private int numberOfDiggingInteractions = 0;
        private readonly float diggingSpeedIncreasePerInteraction = 0.4f;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;
    }
}
