using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    public class OgopogoTrophyItem : BiodiverseVariantItem
    {
        public AudioSource danceAudio;
        public AudioClip[] danceClips;
        public Animator[] trophyAnimators;
        public ScanNodeProperties scanNode;
        public string[] scanNames;

        private int lastClipPlayedId = 4;
        private readonly int chanceToPlayOnEquip = 10;


        public override void OnNewVariantSelected()
        {
            transform.GetChild(Mathf.Abs(ActualVariantID - 1))?.gameObject?.SetActive(false);
            transform.GetChild(ActualVariantID)?.gameObject?.SetActive(true);
            if (scanNames.Length > ActualVariantID)
            {
                scanNode.headerText = scanNames[ActualVariantID];
            }
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (playerHeldBy == null || danceAudio.isPlaying)
                return;
            PlayNextDanceAudioServerRpc();
        }

        public override void EquipItem()
        {
            base.EquipItem();
            if (IsServer && !danceAudio.isPlaying && Random.Range(0, 100) < chanceToPlayOnEquip)
            {
                PlayNextDanceAudioServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlayNextDanceAudioServerRpc()
        {
            PlayNextDanceAudioClientRpc();
        }

        [ClientRpc]
        private void PlayNextDanceAudioClientRpc()
        {
            if (lastClipPlayedId == 4)
            {
                lastClipPlayedId = 0;
            }
            float volume = 0.5f;
            int audioId = ActualVariantID == 0 ? ++lastClipPlayedId : 0;
            danceAudio.PlayOneShot(danceClips[audioId], volume);
            trophyAnimators[ActualVariantID].SetTrigger(ActualVariantID == 0 ? "PlayOgopogo" : "PlaySkeleton");
            WalkieTalkie.TransmitOneShotAudio(danceAudio, danceClips[audioId], volume);
            RoundManager.Instance.PlayAudibleNoise(transform.position, danceAudio.maxDistance, volume, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
            playerHeldBy?.timeSinceMakingLoudNoise = 0f;
        }
    }
}
