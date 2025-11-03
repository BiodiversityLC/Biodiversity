using Biodiversity.Creatures.Aloe;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Rock
{
    public class RockAI : BiodiverseAI
    {
        bool running = false;
        float breathTimer = 0f;
        float timeSinceStartedRunning = 0f;

        public override void Start()
        {
            base.Start();
            SetDestinationToPosition(transform.position);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (IsServer && running && timeSinceStartedRunning > 2f)
            {
                BiodiversityPlugin.LogVerbose($"Rock run distance: {agent.remainingDistance}");
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

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (IsServer && !running)
            {
                creatureAnimator.SetTrigger("Hit");
            }
        }

        public void StartRunning()
        {
            if (!IsServer) return;
            if (running) return;
            GameObject[] nodes = AloeSharedData.Instance.GetOutsideAINodes();
            SetDestinationToPosition(nodes[UnityEngine.Random.Range(0, nodes.Length)].transform.position);
            running = true;
        }
    }
}
