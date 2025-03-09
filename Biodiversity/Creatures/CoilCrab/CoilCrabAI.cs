using GameNetcodeStuff;
using System.Text.RegularExpressions;
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

        public ScanNodeProperties ScanNode;

        public AudioClip DeathSound;

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
            Shell = ShellObject.GetComponent<GrabbableObject>();

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

        public void CustomKillEnemy()
        {
            KillEnemy();
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
                creatureAnimator.SetBool("Walking", false);
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
                        SetDestinationToPosition(RoundManager.Instance.GetNavMeshPosition(selectedPlayer.transform.position, RoundManager.Instance.navHit, 2.75f));
                    } else
                    {
                        agent.speed = 0;
                        agent.angularSpeed = 0;
                        creatureAnimator.SetBool("Walking", false);
                        creatureAnimator.SetBool("Exploding", false);
                        SetDestinationToPosition(transform.position);
                    }
                    break;
                case (int)State.EXPLODE:
                    // Do not touch the magic 1.6f
                    agent.acceleration = CoilCrabHandler.Instance.Config.RunSpeed * 1.6f;
                    agent.speed = CoilCrabHandler.Instance.Config.RunSpeed;
                    agent.angularSpeed = 120;
                    creatureAnimator.SetBool("Walking", true);
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
