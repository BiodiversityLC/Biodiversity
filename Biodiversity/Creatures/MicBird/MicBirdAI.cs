using System;
using UnityEngine;
using Unity.Netcode;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAI : BiodiverseAI
    {
        private enum State
        {
            GOTOSHIP,
            PERCH,
            CALL
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
        private bool setDestCalledAlready = false;
        MalfunctionID malfunction;
        Vector3 targetPos = Vector3.zero;

        private int malfunctionTimes = 0;

        private static MicBirdAI firstSpawned = null;

        public override void Start()
        {
            base.Start();
            if (firstSpawned == null)
            {
                firstSpawned = this;
            }
            agent.Warp(findFarthestNode().position);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (firstSpawned != null)
            {
                firstSpawned = null;
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
            malfunctionInterval -= Time.deltaTime;

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

        public override void DoAIInterval()
        {
            base.DoAIInterval();

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
            }
        }
    }
}
