using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;


namespace Biodiversity.Creatures.WaxSoldier
{
    public class WaxSoldierAI : EnemyAI
    {
        public NetworkVariable<int> State = new NetworkVariable<int>(0);
        public Vector3 StartPosition;
        public Quaternion StartRotation;
        private AudioSource Theme;
        public NavMeshPath path;
        public override void Start()
        {
            path = new NavMeshPath();
            Theme = GetComponent<AudioSource>();
            StartPosition = transform.position;
            StartRotation = transform.rotation;
            NetworkBehaviour.Destroy(gameObject);
        }
        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (State.Value == 0)
            {
                TargetClosestPlayer(1.5f, true);
                if (agent.remainingDistance <= 0.5f)
                {
                    creatureAnimator.SetInteger("state", 0);
                    transform.rotation = StartRotation;
                }
                SetDestinationToPosition(StartPosition);
                if (Theme.isPlaying)
                    Theme.Stop();
                if (targetPlayer)
                {
                    if (Vector3.Distance(gameObject.transform.position, targetPlayer.gameObject.transform.position) <= 15)
                    {
                        State.Value = 1;
                    }
                }

            }
            if (State.Value == 1)
            {
                bool pathed = TargetClosestPlayer();
                creatureAnimator.SetInteger("state", 1);
                if (targetPlayer)
                {
                    SetMovingTowardsTargetPlayer(targetPlayer);
                    if (Vector3.Distance(gameObject.transform.position, targetPlayer.gameObject.transform.position) >= 20 && pathed)
                    {
                        State.Value = 0;
                    }
                    else
                    {
                        if (!Theme.isPlaying)
                            Theme.Play();
                    }
                }
            }
        }
        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        }
    }
}
