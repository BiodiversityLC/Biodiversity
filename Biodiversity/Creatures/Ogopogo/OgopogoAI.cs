using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.General;
using Biodiversity.Util.Scripts;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Ogopogo
{
    internal class OgopogoAI : BiodiverseAI
    {
        private enum State
        {
            Wandering,
            Chasing,
            Rising,
            Goingdown,
            Reset
        }

        // Variables related to water
        private QuicksandTrigger water;
        private readonly QuicksandTrigger[] sandAndWater = FindObjectsOfType<QuicksandTrigger>();
        private readonly List<QuicksandTrigger> waters = [];

        // Movement
        private const float WanderSpeed = 3.5f;
        private const float ChaseSpeed = 4f;
        private float detectionRange;
        private float loseRange;
        private float attackDistance;
        private const float RiseSpeed = 75f;
        private const float RiseHeight = 100f;
        [SerializeField] private Transform RaycastPos;
        private bool wallInFront;
        private float attackTimer;
        private bool dropRaycast;

        // Wander vars
        private float wanderTimer;
        private Vector3 wanderPos = Vector3.zero;

        // Spline control
        [SerializeField] private Transform SplineEnd;
        [SerializeField] private SplineObject splineObject;
        [NonSerialized] private bool splineDone;
        private const float SplineSpeed = 0.7f;
        private bool playerHasBeenGrabbed;
        [SerializeField] private Transform GrabPos;

        // Player references
        private readonly NullableObject<PlayerControllerB> playerGrabbed = new();
        private readonly NullableObject<PlayerControllerB> chasedPlayer = new();
        private CachedDictionary<ulong, MeshRenderer> playerVisorRenderers;
        private float[] _playerDistances;

        // Audio
        [SerializeField] private AudioClip warning;
        [SerializeField] private AudioClip emerge;
        [SerializeField] private AudioClip returnToWater;
        [SerializeField] private KeepY normalAudio;

        // Timer for reset state
        private float resetTimer;

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

            /*
            foreach (SelectableLevel level in StartOfRound.Instance.levels)
            {
                Plugin.Log.LogInfo(level.PlanetName);
                foreach (SpawnableEnemyWithRarity enemy in level.DaytimeEnemies)
                {
                    Plugin.Log.LogInfo(enemy.enemyType.enemyName);
                }
            }
            */

            _playerDistances = new float[StartOfRound.Instance.allPlayerScripts.Length];

            // Loop through all triggers and get all the water
            try
            {
                foreach (QuicksandTrigger maybeWater in sandAndWater)
                {
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater);
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater.isWater);
                    // BiodiversityPlugin.Logger.LogInfo(maybeWater.gameObject.CompareTag("SpawnDenialPoint"));
                    //BiodiversityPlugin.Logger.LogInfo(maybeWater.transform.root.gameObject.name);
                    if (maybeWater.isWater && !maybeWater.gameObject.CompareTag("SpawnDenialPoint") && maybeWater.transform.root.gameObject.name != "Systems")
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
                water = waters[Random.Range(0, waters.Count)];
                transform.position = water.transform.position;

                bool usedPredefinedPos = false;

                switch (StartOfRound.Instance.currentLevel.sceneName)
                {
                    case "Level3Vow":
                    {
                        usedPredefinedPos = true;

                        int vowrand = Random.Range(0, 2);
                        if (vowrand == 0)
                        {
                            transform.position = new Vector3(-104.800003f, -22.0610008f, 110.330002f);
                        }
                        else
                        {
                            transform.position = new Vector3(27f, -22.0610008f, -61.2000008f);
                        }

                        break;
                    }
                    case "Level10Adamance":
                    {
                        usedPredefinedPos = true;

                        int adarand = Random.Range(0, 2);
                        if (adarand == 0)
                        {
                            transform.position = new Vector3(58.1199989f, -11.04f, -1.85000002f);
                        }
                        else
                        {
                            transform.position = new Vector3(52.0800018f, -11.04f, -12.5900002f);
                        }

                        break;
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

                SetWanderPos();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            // Set default y pos of audio
            normalAudio.Init();

            playerVisorRenderers = new CachedDictionary<ulong, MeshRenderer>(playerId =>
                StartOfRound.Instance.allPlayerScripts[playerId].localVisor.gameObject
                    .GetComponentsInChildren<MeshRenderer>()[0]);
        }

        public override void Update()
        {
            base.Update();

            MapDot.position = StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory ? transform.position : new Vector3(transform.position.x, StartOfRound.Instance.mapScreen.targetedPlayer.transform.position.y, transform.position.z);

            skinnedMeshRenderers[0].enabled = !GameNetworkManager.Instance.localPlayerController.isInsideFactory;

            switch (currentBehaviourStateIndex)
            {
                // Step timers
                case (int)State.Wandering:
                    wanderTimer += Time.deltaTime;
                    break;
                
                case (int)State.Reset:
                    resetTimer += Time.deltaTime;
                    break;
                
                case (int)State.Chasing:
                    attackTimer -= Time.deltaTime;
                    break;
            }

            // Set eye position to handle stun. (Calculated on both client and server)
            if (currentBehaviourStateIndex == (int)State.Wandering || currentBehaviourStateIndex == (int)State.Chasing)
            {
                eye.position = defaultEye.transform.position;
                eye.rotation = defaultEye.transform.rotation;
            } 
            else if (chasedPlayer.IsNotNull)
            {
                eye.position = chasedPlayer.Value.transform.position + chasedPlayer.Value.transform.forward * 1;
                TurnObjectTowardsLocation(chasedPlayer.Value.transform.position, eye);
            }
        }

        public void LateUpdate()
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                _playerDistances[player.playerClientId] = Distance2d(StartOfRound.Instance.shipBounds.gameObject, player.gameObject);
            }

        }

        // Use Physics.Raycast they said. It would be fun they said.
        public void FixedUpdate()
        {
            wallInFront = CheckForWall();
            dropRaycast = Physics.Raycast(GrabPos.position, Vector3.down, 20f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
            
            // Move the grabbed player
            if (playerGrabbed.IsNotNull)
            {
                playerGrabbed.Value.transform.position = GrabPos.position;
                playerGrabbed.Value.transform.rotation = GrabPos.rotation;
                
                if (playerGrabbed.Value.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId)
                {
                    playerGrabbed.Value.thisPlayerModelArms.enabled = false;
                    playerVisorRenderers[playerGrabbed.Value.actualClientId].enabled = false;
                }
        
        
                if (!GameNetworkManager.Instance.localPlayerController.isPlayerDead)
                    GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = true;
                playerVisorRenderers[playerGrabbed.Value.actualClientId].enabled = true;
            }
        }

        // Set wander position. (Only matters when run on server)
        private void SetWanderPos()
        {
            BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();
            wanderPos.x = Random.Range(collider.bounds.min.x, collider.bounds.max.x);
            wanderPos.y = water.transform.position.y;
            wanderPos.z = Random.Range(collider.bounds.min.z, collider.bounds.max.z);

            wanderTimer = 0f;
        }

        // Get the closest player in 2d space
        private PlayerControllerB GetClosestPlayer()
        {
            PlayerControllerB ret = null;
            float smallestDistance = 0f;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.transform.position.y >= transform.position.y || currentBehaviourStateIndex == (int)State.Rising)
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
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                // BiodiversityPlugin.Logger.LogInfo(PlayerDistances[0]);
                if (Distance2d(player.gameObject, gameObject) < range &&
                    (player.transform.position.y >= transform.position.y ||
                     currentBehaviourStateIndex == (int)State.Rising) && _playerDistances[player.playerClientId] > 15)
                    return true;
            }

            return false;
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
            SplineEnd.position = chasedPlayer.Value.transform.position;
            splineDone = splineObject.UpdateForward(SplineSpeed, hostDelta);
        }

        // Update the IK object backwards along the spine
        [ClientRpc]
        public void UpdateBackwardClientRpc(float hostDelta)
        {
            splineDone = splineObject.UpdateBackward(SplineSpeed, hostDelta);
        }

        // Set the chased player
        [ClientRpc]
        public void SetPlayerChasedClientRpc(int playerID)
        {
            chasedPlayer.Value = StartOfRound.Instance.allPlayerScripts[playerID];
        }

        // Set the grabbed player or set as null
        [ClientRpc]
        public void SetPlayerGrabbedClientRpc(int playerID, bool setNull = false, bool resetSpecialPlayer = true)
        {
            if (!setNull)
            {
                playerGrabbed.Value = StartOfRound.Instance.allPlayerScripts[playerID];
                playerHasBeenGrabbed = true;
            }
            else
            {
                playerGrabbed.Value = null;
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
            AudioClip audio = id switch
            {
                0 => warning,
                1 => emerge,
                2 => returnToWater,
                _ => warning
            };

            creatureVoice.PlayOneShot(audio);
        }

        private bool CheckForWall()
        {
            return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 7.5f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
        }

        // Reset enemy variables
        private void ResetEnemy()
        {
            playerGrabbed.Value = null;
            chasedPlayer.Value = null;
            splineDone = false;
            wanderTimer = 0f;
            wanderPos = Vector3.zero;
            playerHasBeenGrabbed = false;
            resetTimer = 0f;
            SwitchToBehaviourClientRpc((int)State.Wandering);
            SetPlayerGrabbedClientRpc(0, true);
            creatureAnimator.SetInteger(AnimID, 1);
        }

        private void SpawnVermin()
        {
            foreach (int i in Enumerable.Range(0, 4))
            {
                GameObject vermin = Instantiate(OgopogoHandler.Instance.Assets.VerminEnemyType.enemyPrefab, transform.position, Quaternion.Euler(new Vector3(0, 0, 0)));
                vermin.GetComponentInChildren<NetworkObject>().Spawn(true);
                VerminAI aIscript = vermin.gameObject.GetComponent<VerminAI>();
                aIscript.SetWater = water;
                aIscript.SpawnedByOgo = true;
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
            if (isEnemyDead || !IsServer) return;
            
            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            if (currentBehaviourStateIndex != (int)State.Reset && currentBehaviourStateIndex != (int)State.Goingdown)
            {
                player.inAnimationWithEnemy = this;
                player.inSpecialInteractAnimation = true;

                inSpecialAnimationWithPlayer = player;

                TakeOutOfTruckClientRpc();
                SetPlayerGrabbedClientRpc((int)player.playerClientId, false, false);
                creatureAnimator.SetInteger(AnimID, 3);
            }
        }

        
        public void DisableIK()
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
                if (IsServer)
                    DisableIK();
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

            creatureAnimator.SetBool(Stun, false);
            
            switch (currentBehaviourStateIndex)
            {
                case (int)State.Wandering:
                    float step1 = WanderSpeed * Time.deltaTime;
                    if (wanderTimer >= 5)
                    {
                        SetWanderPos();
                    }

                    TurnTowardsLocation(wanderPos);

                    // I doubt this works with flooding.
                    if (wallInFront)
                    {
                        // BiodiversityPlugin.Logger.LogInfo("Found wall while wandering");
                        SetWanderPos();
                    } 
                    else
                    {
                        transform.position = Vector3.MoveTowards(transform.position, wanderPos, step1);
                    }

                    if (PlayerCheck(detectionRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.Chasing);
                        attackTimer = 3f;
                        PlayVoiceClientRpc(0);
                    }
                    break;
                
                case (int)State.Chasing:
                    float step2 = ChaseSpeed * Time.deltaTime;
                    PlayerControllerB player = GetClosestPlayer();

                    SetPlayerChasedClientRpc((int)player.playerClientId);
                    chasedPlayer.Value = player;

                    Vector3 newLocation = Vector3.MoveTowards(transform.position, new Vector3(player.gameObject.transform.position.x, water.transform.position.y, player.gameObject.transform.position.z), step2);

                    BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();

                    if (player == null)
                    {
                        SwitchToBehaviourClientRpc((int)State.Wandering);
                        return;
                    }

                    TurnTowardsLocation(player.gameObject.transform.position);

                    if (Collision2d(newLocation, collider) && !wallInFront)
                    {
                        transform.position = newLocation;
                    }
                    
                    if (Distance2d(gameObject, player.gameObject) <= attackDistance && attackTimer < 0)
                    {
                        SwitchToBehaviourClientRpc((int)State.Rising);
                        PlayVoiceClientRpc(1);
                        creatureAnimator.SetInteger(AnimID, 2);
                        SpawnVermin();
                    }
                    
                    if (!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.Wandering);
                    }
                    
                    break;
                
                case (int)State.Rising:
                    if (transform.position.y - water.gameObject.transform.position.y < RiseHeight)
                    {
                        Rise(RiseSpeed);
                        TurnTowardsLocation(GetClosestPlayer().gameObject.transform.position);
                    }

                    if (playerHasBeenGrabbed)
                    {
                        splineDone = true;
                    }

                    if (!splineDone && transform.position.y - water.gameObject.transform.position.y > 0.75f * RiseHeight)
                    {
                        UpdateForwardClientRpc(Time.deltaTime);
                    }

                    if(!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.Goingdown);
                        PlayVoiceClientRpc(2);
                        splineDone = false;
                    }

                    if (!(transform.position.y - water.gameObject.transform.position.y < RiseHeight) && splineDone)
                    {
                        transform.position = new Vector3(transform.position.x, water.transform.position.y + RiseHeight, transform.position.z);
                        SwitchToBehaviourClientRpc((int)State.Goingdown);
                        PlayVoiceClientRpc(2);
                        splineDone = false;
                    }
                    break;
                
                case (int)State.Goingdown:
                    if (!splineDone)
                    {
                        UpdateBackwardClientRpc(Time.deltaTime);
                    }

                    if (transform.position.y - water.gameObject.transform.position.y > 0f && splineDone)
                    {
                        GoDown(RiseSpeed);
                    }
                    
                    if (playerGrabbed.IsNotNull && dropRaycast && splineDone)
                    {
                        // BiodiversityPlugin.Logger.LogInfo("dropping player");
                        playerGrabbed.Value.fallValue = 0f;
                        playerGrabbed.Value.fallValueUncapped = 0f;
                        SetPlayerGrabbedClientRpc(0, true);
                        inSpecialAnimationWithPlayer = null;
                    }

                    if (!(transform.position.y - water.gameObject.transform.position.y > 0f) && splineDone)
                    {
                        transform.position = new Vector3(transform.position.x, water.transform.position.y, transform.position.z);
                        SwitchToBehaviourClientRpc((int)State.Reset);
                        SetPlayerGrabbedClientRpc(0, true);
                    }
                    break;
                
                case (int)State.Reset:
                    if (resetTimer >= 12)
                    {
                        ResetEnemy();
                    }
                    break;
            }
        }
    }
}
