using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Rock
{
    public class RockAI : BiodiverseAI
    {
        bool running = false;
        float breathTimer = 0f;
        float timeSinceStartedRunning = 0f;

        public Material[] RockMats;
        public GameObject[] Skins;

        public AudioClip[] Trucksounds;
        public ParticleSystem TruckParticles;

        public static Dictionary<int, GameObject> selectednodes = new();
        public static int nextrockid = 0;

        int rockid;
        bool dead = false;

        MeshRenderer renderer;

        public override void Start()
        {
            base.Start();
            GameObject rockSkin = Skins[Random.Range(0, Skins.Length)];
            rockSkin.SetActive(true);

            if (RockHandler.Instance.chosenMats.ContainsKey(StartOfRound.Instance.currentLevel.name))
            {
                renderer = rockSkin.GetComponent<MeshRenderer>();
                Material[] materials = renderer.materials;
                materials[0] = RockMats[RockHandler.Instance.chosenMats[StartOfRound.Instance.currentLevel.name]];
                renderer.materials = materials;
            }
            rockid = nextrockid;
            nextrockid++;

            SetDestinationToPosition(transform.position);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (IsServer && running && timeSinceStartedRunning > 2f && !agent.pathPending)
            {
                LogVerbose($"Rock run distance: {agent.remainingDistance}");
                if (agent.remainingDistance < 1)
                {
                    creatureAnimator.SetTrigger("Brake");
                    running = false;
                    timeSinceStartedRunning = 0f;
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (IsServer)
            {
                if (running)
                {
                    timeSinceStartedRunning += Time.deltaTime;
                }

                breathTimer += Time.deltaTime;
                if (breathTimer >= 15f)
                {
                    breathTimer = 0f;
                    creatureAnimator.SetTrigger("TakeBreath");
                }
            }
        }

        IEnumerator DeathSequence()
        {
            AudioClip clip = Trucksounds[Random.Range(0, Trucksounds.Length)];
            creatureVoice.PlayOneShot(clip, RockHandler.Instance.Config.RockActiveVolume);
            TruckParticles.Play();
            dead = true;
            System.Array.ForEach(meshRenderers, x => x.enabled = false);
            renderer.enabled = false;
            GetComponent<Collider>().enabled = false;
            yield return new WaitForSeconds(clip.length);
            KillEnemyOnOwnerClient();
            yield return null;
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            LogVerbose($"Rock hit with hitID: {hitID}");
            if (hitID == 331 || force == 12)
            {
                StartCoroutine(DeathSequence());
                return;
            }
            if (IsServer && !running)
            {
                running = true;
                creatureAnimator.SetTrigger("Hit");
            }
        }

        public void StartRunning()
        {
            if (!IsServer) return;
            List<GameObject> nodes = CachedOutsideAINodes.Value;

            if (nodes == null || nodes.Count == 0)
            {
                // Maybe do a LogVerbose("Couldn't get outside nodes") or some other message
            }
            else
            {
                bool foundnode = false;

                while (!foundnode)
                {
                    GameObject node = nodes[UnityEngine.Random.Range(0, nodes.Count)];

                    if (!selectednodes.ContainsValue(node))
                    {
                        foundnode = true;
                        selectednodes[rockid] = node;
                        SetDestinationToPosition(node.transform.position);
                    }
                }
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (!dead)
                selectednodes.Clear();
        }
    }
}