using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilCrabAI : BiodiverseAI
    {
        private enum State
        {
            CREEP,
            EXPLODE
        }

        private GrabbableObject Shell;
        public GameObject ShellPrefab;
        private PlayerControllerB selectedPlayer;

        private float explodeTimer = 0;

        public override void Start()
        {
            base.Start();
            if (!IsServer) return;

            if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Stormy)
            {
                RoundManager.Instance.DespawnEnemyOnServer(gameObject.GetComponent<NetworkObject>());
            }

            GameObject ShellObject = Instantiate(ShellPrefab);
            ShellObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            Shell = ShellObject.GetComponent<GrabbableObject>();

            FindObjectOfType<StormyWeather>().metalObjects.Add(Shell);

            Shell.isInFactory = false;
            Shell.parentObject = transform;
            Shell.SetScrapValue(UnityEngine.Random.RandomRangeInt(Shell.itemProperties.minValue, Shell.itemProperties.maxValue + 1));
            Shell.isHeldByEnemy = true;
            Shell.grabbableToEnemies = false;
            Shell.grabbable = false;
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
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            BiodiversityPlugin.Logger.LogInfo("Hit by " + force + " damage.");

            if (force == 6)
            {
                return;
            }

            SwitchToBehaviourClientRpc((int)State.EXPLODE);
            explodeTimer = UnityEngine.Random.Range(0.4f, 1);

            enemyHP -= force;

            if (enemyHP < 0)
            {
                dropShell();
                KillEnemyOnOwnerClient();
            }
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
                agent.speed = 0;
                agent.angularSpeed = 0;
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
                        SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(selectedPlayer.transform.position, RoundManager.Instance.navHit, 2.75f));
                    } else
                    {
                        agent.speed = 0;
                        agent.angularSpeed = 0;
                        SetDestinationToPosition(transform.position);
                    }
                    break;
                case (int)State.EXPLODE:
                    // Do not touch the magic 1.6f
                    agent.acceleration = CoilCrabHandler.Instance.Config.RunSpeed * 1.6f;
                    agent.speed = CoilCrabHandler.Instance.Config.RunSpeed;
                    agent.angularSpeed = 120;
                    SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(selectedPlayer.transform.position, RoundManager.Instance.navHit, 2.75f));

                    if (explodeTimer <= 0)
                    {
                        SwitchToBehaviourClientRpc((int)State.CREEP);
                        Landmine.SpawnExplosion(transform.position, true, CoilCrabHandler.Instance.Config.DamageRange / 2, CoilCrabHandler.Instance.Config.DamageRange);
                    }

                    break;
            }
        }
    }
}
