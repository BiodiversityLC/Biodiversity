using System;
using UnityEngine;
using Unity.Netcode;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using GameNetcodeStuff;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAI : BiodiverseAI, INoiseListener
    {
        private enum State
        {
            GOTOSHIP,
            PERCH,
            CALL,
            RUN,
            RADARBOOSTER
        }

        private enum SoundID
        {
            CALL
        }

        private enum MalfunctionID
        {
            NONE,
            SHIPDOORS,
            RADARBLINK,
            LIGHTSOUT,
            WALKIE
        }

        [SerializeField] private AudioClip callSound;

        private float callTimer = 30;
        private float malfunctionInterval = 0;
        private float baseMalfunctionInterval = 0;
        private float runTimer = 0;
        private bool setDestCalledAlready = false;
        MalfunctionID malfunction;
        Vector3 targetPos = Vector3.zero;

        RadarBoosterItem distractedRadarBoosterItem = null;
        float distractionTimer = 0;

        private int malfunctionTimes = 0;

        private static MicBirdAI firstSpawned = null;

        public override void Start()
        {
            base.Start();
            if (firstSpawned == null)
            {
                firstSpawned = this;
                enemyType.numberSpawned = 1;
            }
            agent.Warp(findFarthestNode().position);

            HoarderBugAI.RefreshGrabbableObjectsInMapList();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (firstSpawned != null)
            {
                firstSpawned = null;
                enemyType.numberSpawned = 0;
            }
        }

        private Transform findFarthestNode()
        {
            Transform farnode = null;
            float lowestDistance = 0;
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                float nodeDistance = Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position, node.transform.position);
                if (nodeDistance > lowestDistance)
                {
                    lowestDistance = nodeDistance;
                    farnode = node.transform;
                }
            }
            return farnode;
        }

        public override void Update()
        {
            base.Update();
            if (!IsServer) return;
            malfunctionInterval -= Time.deltaTime;

            runTimer -= Time.deltaTime;

            if (currentBehaviourStateIndex == (int)State.RADARBOOSTER)
            {
                distractionTimer -= Time.deltaTime;
            }

            if (runTimer < 0 && currentBehaviourStateIndex == (int)State.RUN)
            {
                setDestCalledAlready = false;
                SwitchToBehaviourClientRpc((int)State.GOTOSHIP);
            }

            if (currentBehaviourStateIndex == (int)State.PERCH)
            {
                callTimer -= Time.deltaTime;
            }

            if (malfunctionInterval < 0)
            {
                bool ranMalfunction = true;
                switch (malfunction)
                {
                    case MalfunctionID.WALKIE:
                        ToggleAllWalkiesOutsideClientRpc();
                        break;
                    case MalfunctionID.SHIPDOORS:
                        ToggleShipDoorsClientRpc();
                        break;
                    case MalfunctionID.RADARBLINK:
                        StartOfRound.Instance.mapScreen.SwitchRadarTargetForward(true);
                        break;
                    case MalfunctionID.LIGHTSOUT:
                        FindObjectOfType<ShipLights>().ToggleShipLights();
                        break;
                    case MalfunctionID.NONE:
                        ranMalfunction = false;
                        break;
                }
                if (ranMalfunction)
                {
                    malfunctionTimes--;
                    if (malfunctionTimes <= 0) malfunction = MalfunctionID.NONE;
                }
                malfunctionInterval = baseMalfunctionInterval;
            }
        }

        [ClientRpc]
        public void PlayVoiceClientRpc(int id)
        {
            AudioClip audio = id switch
            {
                0 => callSound,
                _ => null
            };

            creatureVoice.PlayOneShot(audio);
        }


        [ClientRpc]
        public void ToggleAllWalkiesOutsideClientRpc()
        {
            WalkieTalkie[] walkies = (WalkieTalkie[])Resources.FindObjectsOfTypeAll(typeof(WalkieTalkie));
            foreach (WalkieTalkie walkie in walkies)
            {
                if (walkie.isInFactory) return;
                if (walkie.walkieTalkieLight.enabled == true)
                {
                    walkie.SwitchWalkieTalkieOn(false);
                    continue;
                }
                walkie.SwitchWalkieTalkieOn(true);
            }
        }

        [ClientRpc]
        public void ToggleShipDoorsClientRpc()
        {
            if (StartOfRound.Instance.shipIsLeaving) return;


            HangarShipDoor door = FindObjectOfType<HangarShipDoor>();
            if (door.shipDoorsAnimator.GetBool("Closed"))
            {
                door.shipDoorsAnimator.SetBool("Closed", false);
                return;
            }
            door.shipDoorsAnimator.SetBool("Closed", true);
        }

        [ClientRpc]
        public void TurnRadarBoosterOnClientRpc()
        {
            distractedRadarBoosterItem.EnableRadarBooster(true);
        }

        [ClientRpc]
        public void SyncRadarBoosterClientRpc(NetworkObjectReference radarBooster) {
            NetworkObject obj = null;
            radarBooster.TryGet(out obj);
            distractedRadarBoosterItem = obj.gameObject.GetComponent<RadarBoosterItem>();
        }

        private int negativeRandom(int min, int maxExclusive)
        {
            return Random.Range(min, maxExclusive) * ((Random.Range(0, 2) == 0) ? 1 : -1);
        }

        private void spawnMicBird()
        {
            if (MicBirdHandler.Instance.Assets.MicBirdEnemyType.numberSpawned >= 9) return;


            GameObject bird = Object.Instantiate<GameObject>(MicBirdHandler.Instance.Assets.MicBirdEnemyType.enemyPrefab, Vector3.zero, Quaternion.Euler(Vector3.zero));
            bird.gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            RoundManager.Instance.SpawnedEnemies.Add(bird.GetComponent<EnemyAI>());
            bird.GetComponent<EnemyAI>().enemyType.numberSpawned++;
        }

        private void runAway(float timer)
        {
            if (currentBehaviourStateIndex == (int)State.RUN) return;
            runTimer = timer;
            agent.SetDestination(findFarthestNode().position);
            SwitchToBehaviourClientRpc((int)State.RUN);
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            if (!IsServer) return;
            if (currentBehaviourStateIndex == (int)State.RADARBOOSTER) return;

            BiodiversityPlugin.Logger.LogInfo("The bird heard a sound with id " + noiseID + ". And noise Loundness of " + noiseLoudness + ".");
            if (noiseID == 75 && noiseLoudness >= 0.8 && enemyType.numberSpawned <= 2)
            {
                runAway(20);
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            if (hitID == 1 && IsServer)
            {
                BiodiversityPlugin.Logger.LogInfo("Hit by shovel");
                runAway(40);
            }

            enemyHP -= force;
            if (enemyHP <= 0)
            {
                KillEnemyOnOwnerClient();
                enemyType.numberSpawned--;
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            GameObject maybeRadar = CheckLineOfSight(HoarderBugAI.grabbableObjectsInMap, 60f, 40, 5f, null, null);
            if (maybeRadar)
            {
                if (maybeRadar.GetComponent<GrabbableObject>().GetType() == typeof(RadarBoosterItem))
                {
                    SyncRadarBoosterClientRpc(new NetworkObjectReference(maybeRadar.GetComponent<NetworkObject>()));
                    SwitchToBehaviourClientRpc((int)State.RADARBOOSTER);
                }
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.GOTOSHIP:
                    if (!setDestCalledAlready)
                    {
                        if (firstSpawned == this)
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0);
                            agent.SetDestination(targetPos);
                        }
                        else
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + new Vector3(negativeRandom(2, 7), 4, negativeRandom(2, 7));
                            agent.SetDestination(targetPos);
                        }
                        setDestCalledAlready = true;
                    }
                    if (Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0), transform.position) < 5)
                    {
                        BiodiversityPlugin.Logger.LogInfo("Perching");
                        SwitchToBehaviourClientRpc((int)State.PERCH);
                    }
                    break;
                case (int)State.PERCH:
                    if (callTimer <= 0)
                    {
                        BiodiversityPlugin.Logger.LogInfo("Calling");
                        SwitchToBehaviourClientRpc((int)State.CALL);
                    }
                    break;
                case (int)State.CALL:
                    PlayVoiceClientRpc((int)SoundID.CALL);
                    BiodiversityPlugin.Logger.LogInfo("Caw I'm a bird!");

                    spawnMicBird();

                    callTimer = 60;


                    malfunction = (MalfunctionID)Random.Range(1, Enum.GetValues(typeof(MalfunctionID)).Length);
                    BiodiversityPlugin.Logger.LogInfo("Setting malfunction to " + malfunction.ToString());
                    switch (malfunction)
                    {
                        case MalfunctionID.WALKIE:
                            malfunctionTimes = Random.Range(1, 6);
                            baseMalfunctionInterval = 0.22f;
                            break;
                        case MalfunctionID.SHIPDOORS:
                            malfunctionTimes = Random.Range(1, 6);
                            baseMalfunctionInterval = 0.22f;
                            break;
                        case MalfunctionID.RADARBLINK:
                            malfunctionTimes = Random.Range(1, 8);
                            baseMalfunctionInterval = 0.5f;
                            break;
                        case MalfunctionID.LIGHTSOUT:
                            malfunctionTimes = Random.Range(1, 4);
                            baseMalfunctionInterval = 0.66f;
                            break;
                        default:
                            break;
                    }
                    malfunctionInterval = baseMalfunctionInterval;
                    SwitchToBehaviourClientRpc((int)State.PERCH);
                    break;
                case (int)State.RUN:
                    // Fully handled by the run function so no code here. Just wanted to put it here in case it's used in the future.
                    break;
                case (int)State.RADARBOOSTER:

                    agent.SetDestination(distractedRadarBoosterItem.transform.position);

                    if (Vector3.Distance(transform.position, distractedRadarBoosterItem.transform.position) <= 2 && distractionTimer <= 0)
                    {
                        distractionTimer = Random.Range(1f, 10f);

                        TurnRadarBoosterOnClientRpc();

                        if (Random.Range(0, 2) == 0)
                        {
                            distractedRadarBoosterItem.PlayPingAudioAndSync();
                        }
                        else
                        {
                            distractedRadarBoosterItem.FlashAndSync();
                        }
                    }
                    break;
            }
        }
    }
}
