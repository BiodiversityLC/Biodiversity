using System;
using UnityEngine;
using Unity.Netcode;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using GameNetcodeStuff;
using UnityEngine.AI;
using Biodiversity.Util.SharedVariables;
using BepInEx.Bootstrap;
using System.Linq;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAI : BiodiverseAI, INoiseListener
    {
        private enum State
        {
            WANDER,
            GOTOSHIP,
            PERCH,
            CALL,
            RUN,
            RADARBOOSTER
        }

        private enum SoundID // run and step are exculded because they will be animation synced
        {
            CALL,
            HIT,
            IDLE,
            ROAM,
            SCARED,
            SPAWN
        }

        private enum MalfunctionID
        {
            NONE,
            SHIPDOORS,
            RADARBLINK,
            LIGHTSOUT,
            WALKIE
        }

        // Sound vars start here
        [Header("Sound variables")]

        // Call sounds
        [SerializeField] private AudioClip callSound;
        [SerializeField] private AudioClip leaderCallSound;

        // Hit sounds
        [SerializeField] private AudioClip hitSound;
        
        // Idle sounds
        [SerializeField] private AudioClip[] idleSounds;

        // Roam sounds (only used by leader/first spawned)
        [SerializeField] private AudioClip[] roamSounds;

        // Run sounds
        [SerializeField] public AudioClip[] runSounds;

        // Scared
        [SerializeField] private AudioClip[] scaredSounds;

        // Spawn sound
        [SerializeField] private AudioClip spawnSound;

        // Step sounds
        [SerializeField] public AudioClip[] stepSounds;

        // Singing sounds
        [SerializeField] public AudioClip[] singSounds;

        // Used for checking which step sounds to play depending on if it is running or not.
        public bool running = false;

        private float idleTimer = 10;
        private float roamTimer = 10;

        // malfunction sound variables
        private static AudioSource radarScreenAudio;
        private static AudioSource shipDoorsAudio;

        public AudioClip radarMalClip;
        public AudioClip teleporterMalClip;
        public AudioClip shipDoorsMalClip;
        public AudioClip radarBoosterMalClip;
        // Sound vars stop here

        // Basic mechanic vars
        private float callTimer = 30;
        private float malfunctionInterval = 0;
        private float baseMalfunctionInterval = 0;
        private float runTimer = 0;
        private Vector3 spawnPosition;
        private bool setDestCalledAlready = false;
        MalfunctionID malfunction;
        Vector3 targetPos = Vector3.zero;

        // Wander vars
        private AISearchRoutine wander = new AISearchRoutine();
        private bool wanderingAlready = false;
        private float wanderTimer = 1f;
        private static System.Random spawnRandom;

        // Distraction vars
        RadarBoosterItem distractedRadarBoosterItem = null;
        float distractionTimer = 0;
        float defaultStoppingDistance;

        // Number of times the malfunction will occur
        private int malfunctionTimes = 0;

        // Used for leader mechanic
        private static MicBirdAI firstSpawned = null;

        // Spawn anim
        private float spawnTimer = 2f;
        private bool spawnDone = false;

        // Compatibility mode
        private bool hurt = false;
        private bool compatMode = false;
        private static bool sideSet = false;
        private static bool compatSide = false; // I don't care what false and true map to it works. The middle node is turned all weird so forward is either left or right.

        public override void Start()
        {
            base.Start();
            if (firstSpawned == null)
            {
                firstSpawned = this;
                UpdateNumberSpawnedClientRpc(1);
                spawnRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 22);

                radarScreenAudio = GameObject.Find("StartGameLever").GetComponent<AudioSource>();
                shipDoorsAudio = GameObject.Find("HangarDoorAudioSource").GetComponent<AudioSource>();
            }

            wander.randomized = true;

            spawnPosition = transform.position;

            creatureVoice.volume = (MicBirdHandler.Instance.Config.AudioVolume / 100f) * creatureVoice.volume;

            defaultStoppingDistance = agent.stoppingDistance;

            foreach (var plugin in Chainloader.PluginInfos)
            {
                string GUID = plugin.Value.Metadata.GUID;
                if (MicBirdHandler.Instance.compatGUIDS.Contains(GUID))
                {
                    BiodiversityPlugin.LogVerbose("Micbird compat mode enabled.");
                    compatMode = true;
                    if (!sideSet) {
                        compatSide = Random.Range(0, 2) == 0;
                        sideSet = true;
                    }
                    break;
                }
            }

            HoarderBugAI.RefreshGrabbableObjectsInMapList();

            if (!IsServer) return;
            PlayVoiceClientRpc((int)SoundID.SPAWN, 0);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (firstSpawned != null)
            {
                firstSpawned = null;
                UpdateNumberSpawnedClientRpc(0);
                spawnRandom = null;
            }
        }


        public override void Update()
        { 
            base.Update();

            if (spawnTimer >= 0)
            {
                spawnTimer--;
                if (spawnTimer <= 0)
                {
                    spawnDone = true;
                }
            }
            if (!spawnDone) return;


            if (currentBehaviourStateIndex == (int)State.RUN)
            {
                running = true;
            }
            else
            {
                running = false;
            }
            
            if (!IsServer) return;
            malfunctionInterval -= Time.deltaTime;

            runTimer -= Time.deltaTime;

            if (currentBehaviourStateIndex == (int)State.WANDER)
            {
                wanderTimer -= Time.deltaTime;
                roamTimer -= Time.deltaTime;
            }

            if (currentBehaviourStateIndex == (int)State.RADARBOOSTER)
            {
                distractionTimer -= Time.deltaTime;
            }

            if (runTimer < 0 && currentBehaviourStateIndex == (int)State.RUN)
            {
                SwitchToBehaviourClientRpc((int)State.GOTOSHIP);
            }

            if (currentBehaviourStateIndex == (int)State.PERCH)
            {
                callTimer -= Time.deltaTime;  
                idleTimer -= Time.deltaTime;
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
                        PlayMalfunctionSoundClientRpc(4);
                        ToggleShipDoorsClientRpc();
                        break;
                    case MalfunctionID.RADARBLINK:
                        PlayMalfunctionSoundClientRpc(0);
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
        public void PlayVoiceClientRpc(int id, int rand)
        {
            if (enemyHP <= 0)
            {
                return;
            }

            AudioClip audio;

            switch (id)
            {
                case 0:
                    if (firstSpawned == this)
                    {
                        audio = leaderCallSound;
                        break;
                    }
                    audio = callSound;
                    break;
                case 1:
                    audio = hitSound;
                    break;
                case 2:
                    audio = idleSounds[rand];
                    break;
                case 3:
                    audio = roamSounds[rand];
                    break;
                case 4:
                    audio = scaredSounds[rand];
                    break;
                case 5:
                    audio = spawnSound;
                    break;
                default:
                    audio = null;
                    break;
            }

            creatureVoice.PlayOneShot(audio);
        }

        [ClientRpc]
        public void PlayMalfunctionSoundClientRpc(int id)
        {
            switch (id)
            {
                // radar
                case 0:
                    radarScreenAudio.PlayOneShot(radarMalClip);
                    break;
                // teleporter
                case 1:
                    shipDoorsAudio.PlayOneShot(teleporterMalClip);
                    break;
                // inverseTeleporter
                case 2:
                    shipDoorsAudio.PlayOneShot(teleporterMalClip);
                    break;
                // radar booster
                case 3:
                    distractedRadarBoosterItem.GetComponent<AudioSource>().PlayOneShot(radarBoosterMalClip);
                    break;
                // doors
                case 4:
                    shipDoorsAudio.PlayOneShot(teleporterMalClip);
                    break;
            }
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
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("OutsideAINode");

            Vector3 vector = GameObject.FindGameObjectsWithTag("OutsideAINode")[spawnRandom.Next(0, spawnPoints.Length)].transform.position;
            vector = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(vector, 10f, default(NavMeshHit), spawnRandom, RoundManager.Instance.GetLayermaskForEnemySizeLimit(enemyType));
            vector = RoundManager.Instance.PositionWithDenialPointsChecked(vector, spawnPoints, enemyType);


            GameObject bird = Object.Instantiate<GameObject>(MicBirdHandler.Instance.Assets.MicBirdEnemyType.enemyPrefab, vector, Quaternion.Euler(Vector3.zero));
            bird.gameObject.GetComponentInChildren<NetworkObject>().Spawn(true);
            RoundManager.Instance.SpawnedEnemies.Add(bird.GetComponent<EnemyAI>());
            UpdateNumberSpawnedClientRpc(enemyType.numberSpawned + 1);
        }

        private void runAway(float timer)
        {
            PlayVoiceClientRpc((int)SoundID.SCARED, Random.RandomRangeInt(0, scaredSounds.Length));

            if (currentBehaviourStateIndex == (int)State.RUN) return;
            if (wanderingAlready)
            {
                StopSearch(wander);
                moveTowardsDestination = false;
                wanderingAlready = false;
            }
            runTimer = timer;
            agent.SetDestination(spawnPosition);
            SwitchToBehaviourClientRpc((int)State.RUN);
            setDestCalledAlready = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartRunOnServerServerRpc(float timer)
        {
            runAway(timer);
        }

        [ClientRpc]
        public void UpdateNumberSpawnedClientRpc(int number)
        {
            enemyType.numberSpawned = number;
        }

        [ClientRpc]
        public void CancelTeleportClientRpc()
        {
            TeleporterStatus.CancelTeleport = true;
        }

        [ClientRpc]
        public void CancelInverseTeleportClientRpc()
        {
            TeleporterStatus.CancelInverseTeleport = true;
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            if (currentBehaviourStateIndex == (int)State.RADARBOOSTER) return;

            BiodiversityPlugin.LogVerbose("The Micbird heard a sound with id " + noiseID + ". And noise Loundness of " + noiseLoudness + ".");
            if (noiseID == 75 && noiseLoudness >= 0.8 && enemyType.numberSpawned <= 2)
            {
                agent.speed = 5.4f;
                StartRunOnServerServerRpc(20);
            }
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

            if (hitID == 1 && IsServer)
            {
                if (IsServer && enemyHP > 0) creatureAnimator.SetTrigger("Hurt");
                BiodiversityPlugin.LogVerbose("Micbird hit by shovel");
                agent.speed = 0;
                agent.velocity = Vector3.zero;
                enemyHP -= force;
                hurt = true;
                runAway(40);
            }

            if (enemyHP <= 0 && !isEnemyDead)
            {

                KillEnemyOnOwnerClient();
                enemyType.numberSpawned--;
                if (IsServer)
                {
                    creatureAnimator.SetTrigger("Die"); 
                    creatureAnimator.SetBool("DeadAlready", true);
                }
            }

            if (!IsServer) return;
            PlayVoiceClientRpc((int)SoundID.HIT, 0);
        }

        public void EndHurt()
        {
            hurt = false;
            agent.speed = 5.4f;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (!spawnDone) return;

            GameObject maybeRadar = CheckLineOfSight(HoarderBugAI.grabbableObjectsInMap, 60f, 40, 5f, null, null);
            if (maybeRadar)
            {
                if (maybeRadar.GetComponent<GrabbableObject>().GetType() == typeof(RadarBoosterItem))
                {
                    SyncRadarBoosterClientRpc(new NetworkObjectReference(maybeRadar.GetComponent<NetworkObject>()));
                    SwitchToBehaviourClientRpc((int)State.RADARBOOSTER);
                }
            }

            if (currentBehaviourStateIndex != (int)State.RADARBOOSTER)
            {
                agent.stoppingDistance = defaultStoppingDistance;
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.WANDER:
                    creatureAnimator.SetInteger("ID", 1);

                    if (agent.speed != 3)
                    {
                        agent.speed = 3;
                    }

                    if (firstSpawned != this)
                    {
                        SwitchToBehaviourClientRpc((int)State.GOTOSHIP);
                        return;
                    }
                    if (!wanderingAlready)
                    {
                        BiodiversityPlugin.LogVerbose("Micbird started wandering");
                        StartSearch(transform.position, wander);
                        wanderingAlready = true;
                    }
                    if (roamTimer <= 0)
                    {
                        PlayVoiceClientRpc((int)SoundID.ROAM, Random.RandomRangeInt(0, roamSounds.Length));
                        roamTimer = Random.RandomRangeInt(MicBirdHandler.Instance.Config.BoomBirdIdleMinTime, MicBirdHandler.Instance.Config.BoomBirdIdleMaxTime + 1);
                    }

                    if (wanderTimer <= 0)
                    {
                        if (Random.Range(1, 100) > 95f)
                        {
                            SwitchToBehaviourClientRpc((int)State.GOTOSHIP);
                            return;
                        }
                        wanderTimer = 1f;
                    }

                    break;
                case (int)State.GOTOSHIP:
                    creatureAnimator.SetInteger("ID", 1);


                    if (wanderingAlready)
                    {
                        StopSearch(wander);
                        wanderingAlready = false;
                        return;
                    }

                    if (agent.speed != 3)
                    {
                        agent.speed = 3;
                    }
                    if (!compatMode)
                    {
                        if (firstSpawned == this)
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0);
                        }
                        else
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + new Vector3(negativeRandom(2, 7), 4, negativeRandom(2, 7));
                        }
                    }
                    else
                    {
                        if (firstSpawned == this)
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + (15 * StartOfRound.Instance.shipBounds.transform.forward * (compatSide ? 1 : -1));
                        }
                        else
                        {
                            targetPos = StartOfRound.Instance.middleOfShipNode.position + (Random.Range(13, 19) * StartOfRound.Instance.shipBounds.transform.forward * (compatSide ? 1 : -1)) + (negativeRandom(2, 6) * StartOfRound.Instance.shipBounds.transform.right);
                        }
                    }



                    if (!setDestCalledAlready)
                    {
                        setDestCalledAlready = true;
                        moveTowardsDestination = false;
                        agent.SetDestination(targetPos);
                    }

                    bool AtDestination = false;
                    if (!compatMode)
                    {
                        if (Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position + new Vector3(0, 4, 0), transform.position) < 5)
                        {
                            AtDestination = true;
                        }
                    }
                    else
                    {
                        if (Vector3.Distance(StartOfRound.Instance.middleOfShipNode.position + (15 * StartOfRound.Instance.shipBounds.transform.forward * (compatSide ? 1 : -1)), transform.position) < 10)
                        {
                            AtDestination = true;
                        }
                    }

                    if (AtDestination)
                    {
                        BiodiversityPlugin.LogVerbose("Micbird perching");
                        SwitchToBehaviourClientRpc((int)State.PERCH);
                    }
                    break;
                case (int)State.PERCH:
                    creatureAnimator.SetInteger("ID", 2);

                    if (agent.speed != 3)
                    {
                        agent.speed = 3;
                    }
                    if (callTimer <= 0)
                    {
                        BiodiversityPlugin.LogVerbose("Calling (Micbird)");
                        SwitchToBehaviourClientRpc((int)State.CALL);
                    }
                    if (idleTimer <= 0)
                    {
                        PlayVoiceClientRpc((int)SoundID.IDLE, Random.RandomRangeInt(0, idleSounds.Length));
                        idleTimer = Random.RandomRangeInt(MicBirdHandler.Instance.Config.BoomBirdIdleMinTime, MicBirdHandler.Instance.Config.BoomBirdIdleMaxTime + 1);
                    }
                    break;
                case (int)State.CALL:
                    creatureAnimator.SetInteger("ID", 3);
                    if (firstSpawned == this)
                    {
                        creatureAnimator.SetInteger("CallID", 2);
                    }
                    else
                    {
                        creatureAnimator.SetInteger("CallID", Random.RandomRangeInt(0, 2));
                    }


                    PlayVoiceClientRpc((int)SoundID.CALL, 0);
                    BiodiversityPlugin.LogVerbose("Caw I'm a bird! (Micbird called)");

                    spawnMicBird();

                    callTimer = 60;


                    if (Random.Range(1, 101) <= MicBirdHandler.Instance.Config.TeleportCancelChance)
                    {
                        if (TeleporterStatus.Teleporting)
                        {
                            PlayMalfunctionSoundClientRpc(1);
                            CancelTeleportClientRpc();
                        }
                    }

                    if (Random.Range(1, 101) <= MicBirdHandler.Instance.Config.InverseTeleportCancelChance)
                    {
                        if (TeleporterStatus.TeleportingInverse)
                        {
                            PlayMalfunctionSoundClientRpc(2);
                            CancelInverseTeleportClientRpc();
                        }
                    }

                    int num = Random.Range(1, MicBirdHandler.Instance.totalweight + 1);
                    string malfunctionName = "";
                    foreach (var weight in MicBirdHandler.Instance.weights)
                    {
                        if (num <= weight.Second)
                        {
                            malfunctionName = weight.First;
                            break;
                        }
                    }

                    if (malfunctionName == "")
                    {
                        BiodiversityPlugin.LogVerbose("Something is not working. (Micbird)");
                    }

                    malfunction = Enum.Parse<MalfunctionID>(malfunctionName);
                    BiodiversityPlugin.LogVerbose("Setting Micbird malfunction to " + malfunction.ToString());
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
                            BiodiversityPlugin.LogVerbose("Something is not working. (2) (Micbird)");
                            break;
                    }
                    malfunctionInterval = baseMalfunctionInterval;
                    SwitchToBehaviourClientRpc((int)State.PERCH);
                    break;
                case (int)State.RUN:
                    if (Mathf.Sqrt(Mathf.Pow(agent.velocity.x, 2) + Mathf.Pow(agent.velocity.z, 2)) < 0.2f)
                    {
                        creatureAnimator.SetInteger("ID", 4);
                    }
                    else
                    {
                        creatureAnimator.SetInteger("ID", 1);
                    }
                    break;
                case (int)State.RADARBOOSTER:
                    if (Vector3.Distance(distractedRadarBoosterItem.transform.position, transform.position) >= 2 + MicBirdHandler.Instance.Config.RadarBoosterStopDistance)
                    {
                        creatureAnimator.SetInteger("ID", 1);
                    }
                    else {
                        creatureAnimator.SetInteger("ID", 5);
                    }

                    if (agent.speed != 3)
                    {
                        agent.speed = 3;
                    }


                    agent.stoppingDistance = MicBirdHandler.Instance.Config.RadarBoosterStopDistance;
                    agent.SetDestination(distractedRadarBoosterItem.transform.position);

                    if (!CheckLineOfSightForPosition(distractedRadarBoosterItem.transform.position, 60f, 40, 5f))
                    {
                        setDestCalledAlready = false;
                        SwitchToBehaviourClientRpc((int)State.GOTOSHIP);
                    }

                    if (Vector3.Distance(transform.position, distractedRadarBoosterItem.transform.position) <= 2 + agent.stoppingDistance && distractionTimer <= 0)
                    {
                        distractionTimer = Random.Range(1f, 10f);

                        TurnRadarBoosterOnClientRpc();

                        PlayMalfunctionSoundClientRpc(3);

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
