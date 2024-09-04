using GameNetcodeStuff;
using System;
using System.Collections;
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
        public BoxCollider StabHITBOX;
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
            //NetworkBehaviour.Destroy(gameObject);
        }
        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (State.Value == 0)
            {
                TargetClosestPlayer(1.5f, true,90);
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
                        if (Vector3.Distance(gameObject.transform.position, targetPlayer.gameObject.transform.position) <= 4)
                        {
                            SetDestinationToPosition(transform.position);
                            transform.LookAt(targetPlayer.transform);
                            StartCoroutine(Stab());
                        }
                    }
                }
            }
        }

        public IEnumerator Stab()
        {
            State.Value = 3;
            creatureAnimator.SetInteger("state", 3);
            creatureAnimator.speed = .5f;
            yield return new WaitForSeconds(0.15f);
            StabHITBOX.gameObject.SetActive(true);
            foreach (Collider Obj in Physics.OverlapBox(StabHITBOX.center, StabHITBOX.size, Quaternion.identity))
            {
                var player = Obj.gameObject.GetComponent<PlayerControllerB>();
                if (player)
                {
                    player.DamagePlayer(30,false,true,CauseOfDeath.Stabbing,0,false, transform.forward * 3);
                }
            }
            StabHITBOX.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.3f);
            creatureAnimator.speed = 1f;
            creatureAnimator.SetInteger("state", 0);
            yield return new WaitForSeconds(0.1f);
            State.Value = 0;
        }
        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        }
    }
}
