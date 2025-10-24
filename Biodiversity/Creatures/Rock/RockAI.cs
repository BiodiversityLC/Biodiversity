using Biodiversity.Creatures.Aloe;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Rock
{
    public class RockAI : BiodiverseAI
    {
        bool running = false;
        float breathTimer = 0f;

        public override void Start()
        {
            base.Start();
            SetDestinationToPosition(transform.position);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            bool reachedDestination = Vector3.Distance(agent.destination, transform.position) <= 1;
            if (IsServer && running)
            {
                creatureAnimator.SetBool("Braking", reachedDestination);
                running = !reachedDestination;
            }
        }

        public override void Update()
        {
            base.Update();

            if (IsServer)
            {
                breathTimer += Time.deltaTime;
                if (breathTimer >= 1.5f)
                {
                    breathTimer = 0f;
                    if (Random.Range(0f, 1f) < 0.35f) creatureAnimator.SetTrigger("TakeBreath");
                }
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (IsServer)
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
