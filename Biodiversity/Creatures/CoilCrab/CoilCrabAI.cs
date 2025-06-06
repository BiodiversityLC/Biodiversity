﻿using GameNetcodeStuff;
using System.Text.RegularExpressions;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilCrabAI : BiodiverseAI, IVisibleThreat
    {

        ThreatType IVisibleThreat.type
        {
            get
            {
                // I guess coil crabs are baboon hawks also
                return ThreatType.BaboonHawk;
            }
        }

        int IVisibleThreat.SendSpecialBehaviour(int id)
        {
            return 0;
        }

        int IVisibleThreat.GetThreatLevel(Vector3 seenByPosition)
        {
            if (isEnemyDead) { return 0; }
            return 1;
        }

        int IVisibleThreat.GetInterestLevel()
        {
            return 0;
        }

        Transform IVisibleThreat.GetThreatLookTransform()
        {
            return null;
        }

        Transform IVisibleThreat.GetThreatTransform()
        {
            return transform;
        }

        Vector3 IVisibleThreat.GetThreatVelocity()
        {
            if (base.IsOwner)
            {
                return agent.velocity;
            }
            return Vector3.zero;
        }

        float IVisibleThreat.GetVisibility()
        {
            if (isEnemyDead) { return 0; }
            return 1;
        }

        GrabbableObject IVisibleThreat.GetHeldObject()
        {
            return null;
        }

        bool IVisibleThreat.IsThreatDead()
        {
            return isEnemyDead;
        }




        private enum State
        {
            CREEP,
            EXPLODE
        }

        private GrabbableObject Shell;
        public GameObject ShellPrefab;
        private PlayerControllerB selectedPlayer;

        public ScanNodeProperties ScanNode;

        public AudioClip DeathSound;
        public AudioClip CrabStop;
        public AudioClip CrabShock;

        private float explodeTimer = 0;

        public override void Start()
        {
            base.Start();
            if (!IsServer) return;

            //if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Stormy)
            //{
            //    RoundManager.Instance.DespawnEnemyOnServer(gameObject.GetComponent<NetworkObject>());
            //}

            GameObject ShellObject = Instantiate(ShellPrefab);
            ShellObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            Shell = ShellObject.GetComponent<CoilShell>();

            enemyHP = CoilCrabHandler.Instance.Config.Health;

            if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Stormy)
                FindObjectOfType<StormyWeather>().metalObjects.Add(Shell);

            Regex rg = new Regex(@"^(Min:[0-9]+,Max:[0-9]+)$");
            bool validValues = true;

            if (!rg.IsMatch(CoilCrabHandler.Instance.Config.ItemValue))
            {
                BiodiversityPlugin.Logger.LogWarning("Item values config is invalid defaulting to default values.");
                validValues = false;
            }

            string[] minMax = CoilCrabHandler.Instance.Config.ItemValue.Split(',');
            int min;
            int max;
            if (validValues) {
                min = int.Parse(minMax[0].Split(':')[1]);
                max = int.Parse(minMax[1].Split(':')[1]);
            } else {
                min = 60;
                max = 95;
            }

            HoldItemClientRpc(new NetworkObjectReference(Shell.GetComponent<NetworkObject>()), UnityEngine.Random.Range(min, max));
        }

        [ClientRpc]
        public void HoldItemClientRpc(NetworkObjectReference shell, int value)
        {
            if (!IsServer)
            {
                NetworkObject networkObject;
                shell.TryGet(out networkObject);
                Shell = networkObject.GetComponent<GrabbableObject>();
            }
            Shell.isInFactory = false;
            Shell.parentObject = transform;
            Shell.SetScrapValue(value);
            Shell.isHeldByEnemy = true;
            Shell.grabbableToEnemies = false;
            Shell.grabbable = false;
        }

        [ClientRpc]
        public void SpawnExpolosionClientRpc(Vector3 explosionPosition, float killRange, float damageRange, int nonLethalDamage)
        {
            Landmine.SpawnExplosion(explosionPosition, true, killRange, damageRange, nonLethalDamage);
        }

        [ClientRpc]
        public void StopMovingSoundClientRpc()
        {
            creatureVoice.PlayOneShot(CrabStop);
        }

        [ClientRpc]
        public void ExplodeSoundClientRpc()
        {
            creatureVoice.PlayOneShot(CrabShock);
        }

        public void CustomKillEnemy()
        {
            KillEnemy();
            creatureAnimator.SetInteger("StopAnim", 0);
            creatureVoice.PlayOneShot(DeathSound);
            ScanNode.gameObject.GetComponent<Collider>().enabled = true;
        }

        private void dropShell()
        {
            Shell.isHeldByEnemy = false;
            Shell.grabbableToEnemies = true;
            Shell.grabbable = true;
        }

        private PlayerControllerB nearestPlayer()
        {
            float distance = float.MaxValue;
            PlayerControllerB nearPlayer = null;
            foreach(PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                float distanceTemp = Vector3.Distance(player.transform.position, transform.position);
                if (distanceTemp < distance)
                {
                    distance = distanceTemp;
                    nearPlayer = player;
                }
            }

            return nearPlayer;
        }

        public override void Update()
        {
            base.Update();
            if (this.isEnemyDead)
            {
                return;
            }

            if (!IsServer) return;
            explodeTimer -= Time.deltaTime;
        }

        public void LateUpdate()
        {
            selectedPlayer = nearestPlayer();
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            if (playerWhoHit == null) return;

            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            BiodiversityPlugin.LogVerbose("Coil crab hit by " + force + " damage.");

            if (force == 6)
            {
                return;
            }

            SwitchToBehaviourClientRpc((int)State.EXPLODE);
            explodeTimer = UnityEngine.Random.Range(0.4f, 1);

            bool deadBefore = enemyHP - force > 0;

            enemyHP -= force;

            if (enemyHP <= 0 && !deadBefore)
            {
                CustomKillEnemy();
                creatureAnimator.SetBool("Exploding", false);
                creatureAnimator.SetBool("Walking", false);
                creatureAnimator.SetBool("Dead", true);
                dropShell();
            }
        }

        public void StopAnim()
        {
            creatureAnimator.SetInteger("StopAnim", Random.Range(1, 4));
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
            if (StartOfRound.Instance.allPlayersDead)
            {
                return;
            }
            if (isEnemyDead)
            {
                return;
            }
            if (selectedPlayer == null)
            {
                return;
            }


            //Stop if selected player is inside
            if (selectedPlayer.isInsideFactory)
            {
                if (agent.speed != 0)
                {
                    StopMovingSoundClientRpc();
                }
                agent.speed = 0;
                agent.angularSpeed = 0;
                creatureAnimator.SetBool("Walking", false);
                StopAnim();
                SetDestinationToPosition(transform.position);
                return;
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.CREEP:
                    if (!selectedPlayer.HasLineOfSightToPosition(transform.position) && Vector3.Distance(selectedPlayer.transform.position, transform.position) > 3f)
                    {
                        // Do not touch the magic 1.8f
                        agent.acceleration = CoilCrabHandler.Instance.Config.CreepSpeed * 1.8f;
                        agent.speed = CoilCrabHandler.Instance.Config.CreepSpeed;
                        agent.angularSpeed = 120;
                        creatureAnimator.SetBool("Walking", true);
                        creatureAnimator.SetBool("Exploding", false);
                        creatureAnimator.SetInteger("StopAnim", 0);
                        SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(selectedPlayer.transform.position, RoundManager.Instance.navHit, 2.75f));
                    } else
                    {
                        if (agent.speed != 0)
                        {
                            StopMovingSoundClientRpc();
                        }
                        agent.speed = 0;
                        agent.angularSpeed = 0;
                        creatureAnimator.SetBool("Walking", false);
                        creatureAnimator.SetBool("Exploding", false);
                        StopAnim();
                        SetDestinationToPosition(transform.position);
                    }
                    break;
                case (int)State.EXPLODE:

                    if (agent.acceleration != CoilCrabHandler.Instance.Config.RunSpeed * 1.6f)
                    {
                        ExplodeSoundClientRpc();
                    }

                    // Do not touch the magic 1.6f
                    agent.acceleration = CoilCrabHandler.Instance.Config.RunSpeed * 1.6f;
                    agent.speed = CoilCrabHandler.Instance.Config.RunSpeed;
                    agent.angularSpeed = 120;
                    creatureAnimator.SetBool("Walking", true);
                    creatureAnimator.SetInteger("StopAnim", 0);
                    SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(selectedPlayer.transform.position, RoundManager.Instance.navHit, 2.75f));

                    if (explodeTimer <= 0)
                    {
                        creatureAnimator.SetBool("Walking", false);
                        creatureAnimator.SetBool("Exploding", true);
                        SwitchToBehaviourClientRpc((int)State.CREEP);
                        SpawnExpolosionClientRpc(transform.position, CoilCrabHandler.Instance.Config.KillRange, CoilCrabHandler.Instance.Config.DamageRange, CoilCrabHandler.Instance.Config.ExplosionDamage);
                    }

                    break;
            }
        }
    }
}
