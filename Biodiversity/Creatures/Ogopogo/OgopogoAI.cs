using GameNetcodeStuff;
using Biodiversity.Util.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Biodiversity.General;

namespace Biodiversity.Creatures.Ogopogo
{
    internal class OgopogoAI : BiodiverseAI
    {
        private enum State
        {
            WANDERING,
            CHASING,
            RISING,
            GOINGDOWN,
            RESET
        }

        // Variables related to water
        private QuicksandTrigger water;
        private readonly QuicksandTrigger[] sandAndWater = FindObjectsOfType<QuicksandTrigger>();
        private readonly List<QuicksandTrigger> waters = [];

        // Movement
        private const float wanderSpeed = 3.5f;
        private const float chaseSpeed = 4f;
        private float detectionRange;
        private float loseRange;
        private float attackDistance;
        private const float riseSpeed = 75f;
        private const float riseHeight = 100f;
        [SerializeField] private Transform RaycastPos;
        private bool wallInFront = false;
        private float attackTimer = 0f;
        private bool dropRaycast = false;

        // Wander vars
        private float wanderTimer = 0f;
        private Vector3 wanderPos = Vector3.zero;

        // Spline control
        [SerializeField] private Transform SplineEnd;
        [SerializeField] private SplineObject splineObject;
        [NonSerialized] private bool splineDone = false;
        private const float splineSpeed = 0.7f;
        private bool playerHasBeenGrabbed = false;
        [SerializeField] private Transform GrabPos;

        // Player references
        private PlayerControllerB playerGrabbed = null;
        private PlayerControllerB chasedPlayer;
        float[] PlayerDistances = null;

        // Audio
        [SerializeField] private AudioClip warning;
        [SerializeField] private AudioClip emerge;
        [SerializeField] private AudioClip returnToWater;
        [SerializeField] private KeepY normalAudio;

        // Timer for reset state
        private float resetTimer = 0f;

        // Default position of this.eye (needed for water stun to work)
        [SerializeField] private Transform defaultEye;

        private bool stunnedLastFrame = false;

        // Mapping
        public Transform MapDot;

        public EasyIK IK;
        private static readonly int AnimID = Animator.StringToHash("AnimID");
        private static readonly int Stun = Animator.StringToHash("Stun");

        public override void Start()
        {
            base.Start();

            detectionRange = OgopogoHandler.Instance.Config.DetectionRange;
            loseRange = OgopogoHandler.Instance.Config.LoseRange;
            attackDistance = OgopogoHandler.Instance.Config.AttackDistance;

            /**
            foreach (SelectableLevel level in StartOfRound.Instance.levels)
            {
                Plugin.Log.LogInfo(level.PlanetName);
                foreach (SpawnableEnemyWithRarity enemy in level.DaytimeEnemies)
                {
                    Plugin.Log.LogInfo(enemy.enemyType.enemyName);
                }
            }
            **/
            PlayerDistances = new float[StartOfRound.Instance.allPlayerScripts.Count()];

            // Loop through all triggers and get all the water
            try
            {
                foreach (QuicksandTrigger maybeWater in sandAndWater)
                {
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater);
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater.isWater);
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater.gameObject.CompareTag("SpawnDenialPoint"));
                    if (maybeWater.isWater && !maybeWater.gameObject.CompareTag("SpawnDenialPoint"))
                    {
                        waters.Add(maybeWater);
                    }
                }

                if (waters.Count == 0 || TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Flooded)
                {
                    BiodiversityPlugin.Logger.LogWarning("Despawning because no water exists that is spawnable or there is a flood.");
                    RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(gameObject.GetComponent<NetworkObject>()));
                    return;
                }

                // Set the water he will stay in and teleport to it
                water = waters[UnityEngine.Random.Range(0, waters.Count)];
                transform.position = water.transform.position;

                bool usedPredefinedPos = false;

                if (StartOfRound.Instance.currentLevel.sceneName == "Level3Vow")
                {
                    usedPredefinedPos = true;

                    int vowrand = UnityEngine.Random.Range(0, 2);
                    if (vowrand == 0)
                    {
                        transform.position = new Vector3(-104.800003f, -22.0610008f, 110.330002f);
                    }
                    else
                    {
                        transform.position = new Vector3(27f, -22.0610008f, -61.2000008f);
                    }
                }
                if (StartOfRound.Instance.currentLevel.sceneName == "Level10Adamance")
                {
                    usedPredefinedPos = true;

                    int adarand = UnityEngine.Random.Range(0, 2);
                    if (adarand == 0)
                    {
                        transform.position = new Vector3(58.1199989f, -11.04f, -1.85000002f);
                    }
                    else
                    {
                        transform.position = new Vector3(52.0800018f, -11.04f, -12.5900002f);
                    }
                }

                foreach (var waterd in waters)
                {
                    if (usedPredefinedPos)
                    {
                        if (Collision2d(transform.position, water.GetComponent<BoxCollider>()))
                        {
                            water = waterd;
                        }
                    }
                }



                setWanderPos();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // Set default y pos of audio
            normalAudio.Init();
        }

        public override void Update()
        {
            base.Update();

            if (StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory)
            {
                MapDot.position = transform.position;
            } else
            {
                MapDot.position = new Vector3(transform.position.x, StartOfRound.Instance.mapScreen.targetedPlayer.transform.position.y, transform.position.z);
            }

            if (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
            {
                skinnedMeshRenderers[0].enabled = false;
            }
            else
            {
                skinnedMeshRenderers[0].enabled = true;
            }
            // Step timers
            if (currentBehaviourStateIndex == (int)State.WANDERING)
            {
                wanderTimer += Time.deltaTime;
            }
            if (currentBehaviourStateIndex == (int)State.RESET)
            {
                resetTimer += Time.deltaTime;
            }
            if (currentBehaviourStateIndex == (int)State.CHASING)
            {
                attackTimer -= Time.deltaTime;
            }

            // Set eye position to handle stun. (Calculated on both client and server)
            if (currentBehaviourStateIndex == (int)State.WANDERING || currentBehaviourStateIndex == (int)State.CHASING)
            {
                eye.position = defaultEye.transform.position;
                eye.rotation = defaultEye.transform.rotation;
            } else
            {
                eye.position = chasedPlayer.transform.position + chasedPlayer.transform.forward * 1;
                TurnObjectTowardsLocation(chasedPlayer.transform.position, eye);
            }
        }

        public void LateUpdate()
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                PlayerDistances[player.playerClientId] = Distance2d(StartOfRound.Instance.shipBounds.gameObject, player.gameObject);
            }

        }

        // Use Physics.Raycast they said. It would be fun they said.
        public void FixedUpdate()
        {
            wallInFront = checkForWall();
            dropRaycast = Physics.Raycast(GrabPos.position, Vector3.down, 20f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
            // Move the grabbed player
            if (playerGrabbed != null)
            {
                playerGrabbed.transform.position = GrabPos.position;
                playerGrabbed.transform.rotation = GrabPos.rotation;
            }
            try
            {
                if (playerGrabbed.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId)
                {
                    playerGrabbed.thisPlayerModelArms.enabled = false;
                    playerGrabbed.localVisor.gameObject.GetComponentsInChildren<MeshRenderer>()[0].enabled = false;
                }
            } catch (Exception e) 
            {
                if (!GameNetworkManager.Instance.localPlayerController.isPlayerDead)
                {
                    GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = true;
                }
                GameNetworkManager.Instance.localPlayerController.localVisor.gameObject.GetComponentsInChildren<MeshRenderer>()[0].enabled = true;
            }
        }

        // Set wander position. (Only matters when run on server)
        private void setWanderPos()
        {
            BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();
            wanderPos.x = UnityEngine.Random.Range(collider.bounds.min.x, collider.bounds.max.x);
            wanderPos.y = water.transform.position.y;
            wanderPos.z = UnityEngine.Random.Range(collider.bounds.min.z, collider.bounds.max.z);

            wanderTimer = 0f;
        }

        // Get the closest player in 2d space
        private PlayerControllerB getClosestPlayer()
        {
            PlayerControllerB ret = null;
            float smallestDistance = 0f;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.transform.position.y >= transform.position.y || currentBehaviourStateIndex == (int)State.RISING)
                {
                    if (smallestDistance == 0f)
                    {
                        smallestDistance = Distance2d(player.gameObject, gameObject);
                        ret = player;
                    }
                    else
                    {
                        float distance = Distance2d(player.gameObject, gameObject);

                        if (distance < smallestDistance)
                        {
                            smallestDistance = distance;
                            ret = player;
                        }
                    }
                }
            }
            return ret;
        }

        // Check if players are within a certain range
        private bool PlayerCheck(float range)
        {
            bool ret = false;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                //BiodiversityPlugin.Logger.LogInfo(PlayerDistances[0]);
                if (Distance2d(player.gameObject, gameObject) < range && (player.transform.position.y >= transform.position.y || currentBehaviourStateIndex == (int)State.RISING) && PlayerDistances[player.playerClientId] > 15)
                {
                    ret = true;
                }
            }
            return ret;
        }

        // 2d distance formula
        private static float Distance2d(GameObject obj1, GameObject obj2)
        {
            return Mathf.Sqrt(Mathf.Pow(obj1.transform.position.x - obj2.transform.position.x, 2f) + Mathf.Pow(obj1.transform.position.z - obj2.transform.position.z, 2f));
        }

        // Check if a position is in a collider's 2d space
        private bool Collision2d(Vector3 pos, Collider col)
        {
            return col.bounds.Contains(new Vector3(pos.x, water.transform.position.y, pos.z));
        }

        // Turn towards position
        private void TurnTowardsLocation(Vector3 location)
        {
            transform.LookAt(new Vector3(location.x, transform.position.y, location.z));
        }

        // Turn object toward position
        private static void TurnObjectTowardsLocation(Vector3 location, Transform objectRef)
        {
            objectRef.LookAt(new Vector3(location.x, objectRef.transform.position.y, location.z));
        }

        // Rise at given speed. Enemy AI already updates position across client so no need for RPC
        private void Rise(float speed)
        {
            transform.Translate(Vector3.up * speed * Time.deltaTime);
        }

        // Go down at given speed. Same as above
        private void GoDown(float speed)
        {
            transform.Translate(Vector3.down * speed * Time.deltaTime);
        }

        // Update the IK object forward along the spline.
        [ClientRpc]
        public void UpdateForwardClientRpc(float hostDelta)
        {
            SplineEnd.position = chasedPlayer.transform.position;
            splineDone = splineObject.UpdateForward(splineSpeed, hostDelta);
        }

        // Update the IK object backwards along the spine
        [ClientRpc]
        public void UpdateBackwardClientRpc(float hostDelta)
        {
            splineDone = splineObject.UpdateBackward(splineSpeed, hostDelta);
        }

        // Set the chased player
        [ClientRpc]
        public void SetPlayerChasedClientRpc(int playerID)
        {
            chasedPlayer = StartOfRound.Instance.allPlayerScripts[playerID];
        }

        // Set the grabbed player or set as null
        [ClientRpc]
        public void SetPlayerGrabbedClientRpc(int playerID, bool setNull = false, bool resetSpecialPlayer = true)
        {
            if (!setNull)
            {
                playerGrabbed = StartOfRound.Instance.allPlayerScripts[playerID];
                playerHasBeenGrabbed = true;
            }
            else
            {
                playerGrabbed = null;
                playerHasBeenGrabbed = false;
            }
            try
            {
                if (resetSpecialPlayer)
                {
                    inSpecialAnimationWithPlayer.inAnimationWithEnemy = null;
                    inSpecialAnimationWithPlayer.inSpecialInteractAnimation = false;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // Play sounds
        [ClientRpc]
        public void PlayVoiceClientRpc(int id)
        {
            AudioClip audio;

            if (id == 0)
            {
                audio = warning;
            }
            else if (id == 1)
            {
                audio = emerge;
            }
            else if (id == 2)
            {
                audio = returnToWater;
            }
            else
            {
                audio = warning;
            }

            creatureVoice.PlayOneShot(audio);
        }

        private bool checkForWall()
        {
            return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 7.5f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
        }

        // Reset enemy variables
        private void resetEnemy()
        {
            playerGrabbed = null;
            chasedPlayer = null;
            splineDone = false;
            wanderTimer = 0f;
            wanderPos = Vector3.zero;
            playerHasBeenGrabbed = false;
            resetTimer = 0f;
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            SetPlayerGrabbedClientRpc(0, true);
            creatureAnimator.SetInteger(AnimID, 1);
        }

        private void spawnVermin()
        {
            foreach (var i in Enumerable.Range(0, 4))
            {
                GameObject vermin = Instantiate<GameObject>(OgopogoHandler.Instance.Assets.VerminEnemyType.enemyPrefab, transform.position, Quaternion.Euler(new Vector3(0, 0, 0)));
                vermin.GetComponentInChildren<NetworkObject>().Spawn(true);
                VerminAI AIscript = vermin.gameObject.GetComponent<VerminAI>();
                AIscript.setWater = water;
                AIscript.spawnedByOgo = true;
            }
        }

        [ClientRpc]
        public void TakeOutOfTruckClientRpc()
        {
            VehicleController vehicle = FindObjectOfType<VehicleController>();

            if (vehicle == null)
            {
                return;
            }

            if (vehicle.localPlayerInControl)
            {
                vehicle.ExitDriverSideSeat();
            }

            if (vehicle.localPlayerInPassengerSeat)
            {
                vehicle.ExitPassengerSideSeat();
            }
        }

        // Handle grabbing
        public override void OnCollideWithPlayer(Collider other)
        {
            if (isEnemyDead) { 
                return;
            }
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            if (currentBehaviourStateIndex != (int)State.RESET && currentBehaviourStateIndex != (int)State.GOINGDOWN)
            {
                player.inAnimationWithEnemy = this;
                player.inSpecialInteractAnimation = true;

                inSpecialAnimationWithPlayer = player;

                TakeOutOfTruckClientRpc();
                SetPlayerGrabbedClientRpc((int)player.playerClientId, false, false);
                creatureAnimator.SetInteger(AnimID, 3);
            }
        }

        [ClientRpc]
        public void DisableIKClientRpc()
        {
            IK.enabled = false;
        }

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            // BiodiversityPlugin.Logger.LogInfo("It's running");
            enemyHP -= force;
            if (enemyHP <= 0)
            {
                KillEnemyOnOwnerClient();
                if (IsHost || IsServer)
                {
                    DisableIKClientRpc();
                }
            }
        }


        public override void CancelSpecialAnimationWithPlayer()
        {
            base.CancelSpecialAnimationWithPlayer();
            SetPlayerGrabbedClientRpc(0, true);
            inSpecialAnimationWithPlayer = null;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            // handle stun
            if (stunNormalizedTimer > 0)
            {
                SetPlayerGrabbedClientRpc(0, true);
                creatureAnimator.SetBool(Stun, true);
                return;
            }
            else
            {
                creatureAnimator.SetBool(Stun, false);
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.WANDERING:
                    float step1 = wanderSpeed * Time.deltaTime;
                    if (wanderTimer >= 5)
                    {
                        setWanderPos();
                    }

                    TurnTowardsLocation(wanderPos);

                    // I doubt this works with flooding.
                    if (wallInFront)
                    {
                        // BiodiversityPlugin.Logger.LogInfo("Found wall while wandering");
                        setWanderPos();
                    } else
                    {
                        transform.position = Vector3.MoveTowards(transform.position, wanderPos, step1);
                    }

                    if (PlayerCheck(detectionRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.CHASING);
                        attackTimer = 3f;
                        PlayVoiceClientRpc(0);
                    }
                    break;
                case (int)State.CHASING:
                    float step2 = chaseSpeed * Time.deltaTime;
                    PlayerControllerB player = getClosestPlayer();

                    SetPlayerChasedClientRpc((int)player.playerClientId);
                    chasedPlayer = player;

                    Vector3 newLocation = Vector3.MoveTowards(transform.position, new Vector3(player.gameObject.transform.position.x, water.transform.position.y, player.gameObject.transform.position.z), step2);

                    BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();

                    if (player == null)
                    {
                        SwitchToBehaviourClientRpc((int)State.WANDERING);
                        return;
                    }

                    TurnTowardsLocation(player.gameObject.transform.position);

                    if (Collision2d(newLocation, collider) && !wallInFront)
                    {
                        transform.position = newLocation;
                    }
                    if (Distance2d(gameObject, player.gameObject) <= attackDistance && attackTimer < 0)
                    {
                        SwitchToBehaviourClientRpc((int)State.RISING);
                        PlayVoiceClientRpc(1);
                        creatureAnimator.SetInteger(AnimID, 2);
                        spawnVermin();
                    }
                    if (!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.WANDERING);
                    }
                    break;
                case (int)State.RISING:
                    if (transform.position.y - water.gameObject.transform.position.y < riseHeight)
                    {
                        Rise(riseSpeed);
                        TurnTowardsLocation(getClosestPlayer().gameObject.transform.position);
                    }

                    if (playerHasBeenGrabbed)
                    {
                        splineDone = true;
                    }

                    if (!splineDone && transform.position.y - water.gameObject.transform.position.y > 0.75f * riseHeight)
                    {
                        UpdateForwardClientRpc(Time.deltaTime);
                    }

                    if(!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.GOINGDOWN);
                        PlayVoiceClientRpc(2);
                        splineDone = false;
                    }

                    if ((!(transform.position.y - water.gameObject.transform.position.y < riseHeight)) && splineDone)
                    {
                        transform.position = new Vector3(transform.position.x, water.transform.position.y + riseHeight, transform.position.z);
                        SwitchToBehaviourClientRpc((int)State.GOINGDOWN);
                        PlayVoiceClientRpc(2);
                        splineDone = false;
                    }
                    break;
                case (int)State.GOINGDOWN:
                    if (!splineDone)
                    {
                        UpdateBackwardClientRpc(Time.deltaTime);
                    }

                    if (transform.position.y - water.gameObject.transform.position.y > 0f && splineDone)
                    {
                        GoDown(riseSpeed);
                    }
                    
                    if (playerGrabbed != null && dropRaycast && splineDone)
                    {
                        // BiodiversityPlugin.Logger.LogInfo("dropping player");
                        playerGrabbed.fallValue = 0f;
                        playerGrabbed.fallValueUncapped = 0f;
                        SetPlayerGrabbedClientRpc(0, true);
                        inSpecialAnimationWithPlayer = null;
                    }

                    if (!(transform.position.y - water.gameObject.transform.position.y > 0f) && splineDone)
                    {
                        transform.position = new Vector3(transform.position.x, water.transform.position.y, transform.position.z);
                        SwitchToBehaviourClientRpc((int)State.RESET);
                        SetPlayerGrabbedClientRpc(0, true);
                    }
                    break;
                case (int)State.RESET:
                    if (resetTimer >= 12)
                    {
                        resetEnemy();
                    }
                    break;
            }
        }
    }
}
