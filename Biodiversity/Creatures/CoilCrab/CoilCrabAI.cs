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

        public override void Start()
        {
            base.Start();
            if (!IsServer) return;
            GameObject ShellObject = Instantiate(ShellPrefab);
            ShellObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            Shell = ShellObject.GetComponent<GrabbableObject>();


            Shell.parentObject = this.transform;
            Shell.SetScrapValue(UnityEngine.Random.RandomRangeInt(Shell.itemProperties.minValue, Shell.itemProperties.maxValue + 1));
            Shell.isHeldByEnemy = true;
            Shell.grabbableToEnemies = false;
            Shell.grabbable = false;

            selectPlayer();
        }

        private void dropShell()
        {
            Shell.isHeldByEnemy = false;
            Shell.grabbableToEnemies = true;
            Shell.grabbable = true;
        }

        private void selectPlayer()
        {
            selectedPlayer = StartOfRound.Instance.allPlayerScripts[0];
        }

        public override void Update()
        {
            base.Update();
            if (this.isEnemyDead)
            {
                return;
            }

            if (!IsServer) return;

            if (selectedPlayer.isInsideFactory || !selectedPlayer.IsSpawned || selectedPlayer.isPlayerDead)
            {
                selectPlayer();
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            BiodiversityPlugin.Logger.LogInfo("Hit by " + force + " damage.");

            if (force == 6)
            {
                return;
            }

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

            switch (currentBehaviourStateIndex)
            {
                case (int)State.CREEP:
                    if (selectedPlayer.isInsideFactory)
                    {
                        agent.speed = 0;
                        agent.angularSpeed = 0;
                        SetDestinationToPosition(transform.position);
                        break;
                    }
                     
                    if (!selectedPlayer.HasLineOfSightToPosition(transform.position))
                    {
                        agent.speed = 3.5f;
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
                    break;
            }
        }
    }
}
