using Biodiversity.Items.Developeritems;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Items.DeveloperItems
{
    public class StrangeDogBehaviour : BiodiverseItem
    {
        public Animator mainAnimator;
        public AudioSource mainObjectAudio;
        public AudioClip squeezeSound;
        public AudioClip[] squeezeSoundsRare;
        public AudioSource flyAudio;
        public ParticleSystem flyParticles;

        private int flyEffectChance;
        private int rareSqueezeChance;
        private bool flying = false;
        private Vector3 lastPosition;
        private float positionTimer;
        private RaycastHit itemHit;
        private Ray itemThrowRay;

        public override void Start()
        {
            base.Start();
            flyEffectChance = DeveloperScrapHandler.Instance.Config.StrangeDog?.Get<int>("Fly effect chance") ?? 5;
            rareSqueezeChance = DeveloperScrapHandler.Instance.Config.StrangeDog?.Get<int>("Rare squeeze sfx chance") ?? 10;
        }

        public override void Update()
        {
            base.Update();
            if (!flying)
            {
                return;
            }

            if ((transform.localPosition - lastPosition).magnitude < 0.01f)
            {
                positionTimer += Time.deltaTime;

                if (positionTimer >= 0.1f)
                {
                    Explosion();
                }
            }
            else
            {
                positionTimer = 0f;
            }

            lastPosition = transform.localPosition;
        }

        public override void FallWithCurve()
        {
            if (!flying)
            {
                base.FallWithCurve();
                return;
            }
            float magnitude = (startFallingPosition - targetFloorPosition).magnitude;
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(itemProperties.restingRotation.x, transform.eulerAngles.y, itemProperties.restingRotation.z), 14f * Time.deltaTime / magnitude);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetFloorPosition, magnitude * 1f * Time.deltaTime);
            fallTime += Mathf.Abs(Time.deltaTime * 12f / magnitude);
        }

        public override void ActivatePhysicsTrigger(Collider other)
        {
            base.ActivatePhysicsTrigger(other);
            if (!flying)
            {
                return;
            }
            Explosion();
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (!buttonDown || mainObjectAudio.isPlaying || flyAudio.isPlaying)
            {
                return;
            }
            UseItemServerRpc(!StartOfRound.Instance.inShipPhase && playerHeldBy != null && Random.Range(0, 100) <= flyEffectChance - 1);
        }

        [ServerRpc(RequireOwnership = false)]
        private void UseItemServerRpc(bool flyEffectActivate)
        {
            int squeezeId = -1;
            if (!flyEffectActivate && Random.Range(0, 100) <= rareSqueezeChance - 1)
            {
                squeezeId = Random.Range(0, squeezeSoundsRare.Length);
            }
            UseItemClientRpc(flyEffectActivate, squeezeId);
        }

        [ClientRpc]
        private void UseItemClientRpc(bool flyEffectActivate, int squeezeId)
        {
            if (!flyEffectActivate)
            {
                // squeeze sfx
                float volume = 1f;
                AudioClip clip = squeezeId == -1 ? squeezeSound : squeezeSoundsRare[squeezeId];
                mainObjectAudio.PlayOneShot(clip, volume);
                mainAnimator.SetTrigger("Squeeze");
                WalkieTalkie.TransmitOneShotAudio(mainObjectAudio, clip, volume);
                RoundManager.Instance.PlayAudibleNoise(transform.position, mainObjectAudio.maxDistance, volume, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed);
                playerHeldBy?.timeSinceMakingLoudNoise = 0f;
            }
            else
            {
                // fly explosion animation
                StartCoroutine(FlyEffectAnimation());
            }
        }

        private IEnumerator FlyEffectAnimation()
        {
            flyAudio.volume = 1f;
            flyAudio.Play();
            mainAnimator.SetTrigger("Fly");
            flyParticles.Play();
            lastPosition = transform.localPosition;
            flying = true;
            if (playerHeldBy != null && isHeld)
            {
                if (playerHeldBy.IsOwner)
                {
                    playerHeldBy.DiscardHeldObject(placeObject: true, placePosition: GetItemThrowDestination());
                }
                yield return new WaitForEndOfFrame();
            }
            grabbable = false;
            grabbableToEnemies = false;
            gameObject.GetComponent<BoxCollider>().enabled = false;
            yield return new WaitForSeconds(5f);
            if (flying)
            {
                Explosion();
            }
        }

        private void Explosion()
        {
            flyParticles.Stop();
            flying = false;
            if (!StartOfRound.Instance.inShipPhase)
            {
                Landmine.SpawnExplosion(transform.position, spawnExplosionEffect: true, killRange: 2, damageRange: 7, nonLethalDamage: 20, physicsForce: 15);
                if (IsServer)
                {
                    NetworkObject netObj = gameObject.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
            }
        }

        private Vector3 GetItemThrowDestination()
        {
            itemThrowRay = new(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward);
            float forward = Vector3.Dot(playerHeldBy.gameplayCamera.transform.forward, Vector3.up);
            float throwDistance = Mathf.Lerp(20f, 5f, Mathf.Abs(forward));  // full horizontal to down, can be improved
            Vector3 position = (!Physics.Raycast(itemThrowRay, out itemHit, throwDistance, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore)) ? itemThrowRay.GetPoint(throwDistance) : itemThrowRay.GetPoint(itemHit.distance - 0.05f);
            itemThrowRay = new Ray(position, Vector3.down);
            if (Physics.Raycast(itemThrowRay, out itemHit, 30f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                return itemHit.point + Vector3.up * 0.05f;
            }
            return itemThrowRay.GetPoint(30f);
        }
    }
}
