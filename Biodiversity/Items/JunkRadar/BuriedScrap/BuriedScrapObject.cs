using Biodiversity.Util.DataStructures;
using System.Collections;
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

        private JunkRadarItem masterJunkRadar;
        private bool isEnabled = false;

        public InteractTrigger diggingTrigger;
        public BoxCollider diggingCollider;
        public ParticleSystem diggingParticles;
        public AudioSource diggingAudio;

        public void Update()
        {
            if (!isEnabled && masterJunkRadar != null)
            {
                if (masterJunkRadar.hasBeenHeld)
                {
                    EnableBuriedScrap();
                }
            }
        }

        private void EnableBuriedScrap()
        {
            isEnabled = true;
            gameObject.SetActive(true);
        }

        [ServerRpc]
        public void SyncMasterServerRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
        {
            SyncMasterClientRpc(buriedScrapRef, masterJunkRadarRef);
        }

        [ClientRpc]
        private void SyncMasterClientRpc(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
        {
            StartCoroutine(SyncMaster(buriedScrapRef, masterJunkRadarRef));
        }

        private IEnumerator SyncMaster(NetworkObjectReference buriedScrapRef, NetworkObjectReference masterJunkRadarRef)
        {
            NetworkObject itemNetObject = null;
            masterJunkRadarRef.TryGet(out var masterNetObject);
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 8f && !buriedScrapRef.TryGet(out itemNetObject))
            {
                yield return new WaitForSeconds(0.03f);
            }
            if (itemNetObject == null || masterNetObject == null)
            {
                yield break;
            }
            yield return new WaitForEndOfFrame();
            masterJunkRadar = masterNetObject.GetComponent<JunkRadarItem>();
        }
    }
}
