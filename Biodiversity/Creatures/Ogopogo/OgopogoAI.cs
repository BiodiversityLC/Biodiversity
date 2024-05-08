using GameNetcodeStuff;
using LethalLib.Modules;
using Biodiversity.Util.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using Biodiversity.General;

namespace Biodiversity.Creatures.Enemy
{
    internal class OgopogoAI : BiodiverseAI
    {
        public enum State
        {
            WANDERING,
            CHASING,
            RISING,
            GOINGDOWN,
            RESET
        }

        // Variables related to water
        QuicksandTrigger water;
        QuicksandTrigger[] sandAndWater = GameObject.FindObjectsOfType<QuicksandTrigger>();
        List<QuicksandTrigger> waters = new List<QuicksandTrigger>();

        // Movement
        float wanderSpeed = 3.5f;
        float chaseSpeed = 4f;
        float detectionRange = 60f;
        float loseRange = 70f;
        float attackDistance = 45f;
        float riseSpeed = 75f;
        float riseHeight = 50f;
        [SerializeField] private Transform RaycastPos;

        // Wander vars
        float wanderTimer = 0f;
        Vector3 wanderPos = Vector3.zero;

        // Spline control
        [SerializeField] private Transform SplineEnd;
        [SerializeField] private SplineObject splineObject;
        [NonSerialized] private bool splineDone = false;
        float splineSpeed = 0.7f;

        // Player references
        PlayerControllerB playerGrabbed = null;
        PlayerControllerB chasedPlayer;

        //Audio
        [SerializeField] private AudioClip warning;
        [SerializeField] private AudioClip emerge;
        [SerializeField] private AudioClip returnToWater;
        [SerializeField] private KeepY normalAudio;

        // Timer for reset state
        float resetTimer = 0f;

        // Default position of this.eye (needed for water stun to work)
        [SerializeField]Transform defaultEye;

        public override void Start()
        {
            base.Start();

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

            // Loop through all triggers and get all the water
            try
            {
                foreach (QuicksandTrigger maybeWater in sandAndWater)
                {
                    BiodiversityPlugin.Logger.LogInfo(maybeWater);
                    BiodiversityPlugin.Logger.LogInfo(maybeWater.isWater);
                    if (maybeWater.isWater)
                    {
                        waters.Add(maybeWater);
                    }
                }

                // Set the water he will stay in and teleport to it
                water = waters[UnityEngine.Random.Range(0, waters.Count)];
                transform.position = water.transform.position;
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
            // Step timers
            if (currentBehaviourStateIndex == (int)State.WANDERING)
            {
                wanderTimer += Time.deltaTime;
            }
            if (currentBehaviourStateIndex == (int)State.RESET)
            {
                resetTimer += Time.deltaTime;
            }

            // Set eye position to handle stun. (Calculated on both client and server)
            if (currentBehaviourStateIndex == (int)State.WANDERING || currentBehaviourStateIndex == (int)State.CHASING)
            {
                this.eye.position = defaultEye.transform.position;
                this.eye.rotation = defaultEye.transform.rotation;
            } else
            {
                this.eye.position = chasedPlayer.transform.position + chasedPlayer.transform.forward * 1;
                TurnObjectTowardsLocation(chasedPlayer.transform.position, this.eye);
            }
        }

        public void LateUpdate()
        {
            // Move the grabbed player
            if (playerGrabbed != null)
            {
                playerGrabbed.transform.position = splineObject.transform.position;
                playerGrabbed.transform.rotation = splineObject.transform.rotation;
            }
        }

        // Set wander position. (Only matters when run on server)
        void setWanderPos()
        {
            BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();
            wanderPos.x = UnityEngine.Random.Range(collider.bounds.min.x, collider.bounds.max.x);
            wanderPos.y = water.transform.position.y;
            wanderPos.z = UnityEngine.Random.Range(collider.bounds.min.z, collider.bounds.max.z);

            wanderTimer = 0f;
        }

        // Get the closest player in 2d space
        PlayerControllerB getClosestPlayer()
        {
            PlayerControllerB ret = null;
            float smallestDistance = 0f;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (smallestDistance == 0f)
                {
                    smallestDistance = Distance2d(player.gameObject, this.gameObject);
                    ret = player;
                }
                else
                {
                    float Distance = Distance2d(player.gameObject, this.gameObject);

                    if (Distance < smallestDistance)
                    {
                        smallestDistance = Distance;
                        ret = player;
                    }
                }
            }
            return ret;
        }

        // Check if players are within a certain range
        bool PlayerCheck(float range)
        {
            bool ret = false;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (Distance2d(player.gameObject, this.gameObject) < range)
                {
                    ret = true;
                }
            }
            return ret;
        }

        // 2d distance formula
        float Distance2d(GameObject obj1, GameObject obj2)
        {
            return Mathf.Sqrt(Mathf.Pow(obj1.transform.position.x - obj2.transform.position.x, 2f) + Mathf.Pow(obj1.transform.position.z - obj2.transform.position.z, 2f));
        }

        // Check if a position is in a collider's 2d space
        bool Collision2d(Vector3 pos, BoxCollider col)
        {
            return col.bounds.Contains(new Vector3(pos.x, water.transform.position.y, pos.z));
        }

        // Turn towards position
        void TurnTowardsLocation(Vector3 location)
        {
            this.transform.LookAt(new Vector3(location.x, this.transform.position.y, location.z));
        }

        // Turn object toward position
        void TurnObjectTowardsLocation(Vector3 location, Transform objectRef)
        {
            objectRef.LookAt(new Vector3(location.x, objectRef.transform.position.y, location.z));
        }

        // Rise at given speed. Enemy AI already updates position across client so no need for RPC
        void Rise(float speed)
        {
            this.transform.Translate(Vector3.up * speed * Time.deltaTime);
        }

        // Go down at given speed. Same as above
        void GoDown(float speed)
        {
            this.transform.Translate(Vector3.down * speed * Time.deltaTime);
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
        public void SetPlayerGrabbedClientRpc(int playerID, bool setNull = false)
        {
            if (!setNull)
            {
                playerGrabbed = StartOfRound.Instance.allPlayerScripts[playerID];
            }
            else
            {
                playerGrabbed = null;
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

            this.creatureVoice.PlayOneShot(audio);
        }

        bool checkForWall()
        {
            return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 7.5f, ~(1 << 8) /**Bitmasks are weird. This references layer 8 which is "Room"**/);
        }

        // Reset enemy variables
        void resetEnemy()
        {
            playerGrabbed = null;
            chasedPlayer = null;
            splineDone = false;
            wanderTimer = 0f;
            wanderPos = Vector3.zero;
            resetTimer = 0f;
            SwitchToBehaviourClientRpc((int)State.WANDERING);
            SetPlayerGrabbedClientRpc(0, true);
        }

        // Handle grabbing
        public override void OnCollideWithPlayer(UnityEngine.Collider other)
        {
            if (currentBehaviourStateIndex != (int)State.RESET)
            {
                SetPlayerGrabbedClientRpc((int)other.gameObject.GetComponent<PlayerControllerB>().playerClientId);
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            // handle stun
            if (stunNormalizedTimer > 0)
            {
                SetPlayerGrabbedClientRpc(0, true);
                return;
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
                    if (checkForWall())
                    {
                        setWanderPos();
                    } else
                    {
                        transform.position = Vector3.MoveTowards(transform.position, wanderPos, step1);
                    }

                    if (PlayerCheck(detectionRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.CHASING);
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


                    TurnTowardsLocation(player.gameObject.transform.position);

                    if (Collision2d(newLocation, collider) && !checkForWall())
                    {
                        transform.position = newLocation;
                    }
                    if (Distance2d(this.gameObject, player.gameObject) <= attackDistance)
                    {
                        SwitchToBehaviourClientRpc((int)State.RISING);
                        PlayVoiceClientRpc(1);
                    }
                    if (!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.WANDERING);
                    }
                    break;
                case (int)State.RISING:
                    if (this.transform.position.y - water.gameObject.transform.position.y < riseHeight)
                    {
                        Rise(riseSpeed);
                        TurnTowardsLocation(getClosestPlayer().gameObject.transform.position);
                    }

                    if (!splineDone)
                    {
                        UpdateForwardClientRpc(Time.deltaTime);
                    }

                    if ((!(this.transform.position.y - water.gameObject.transform.position.y < riseHeight)) && splineDone)
                    {
                        this.transform.position = new Vector3(this.transform.position.x, water.transform.position.y + riseHeight, this.transform.position.z);
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

                    if (this.transform.position.y - water.gameObject.transform.position.y > 0f)
                    {
                        GoDown(riseSpeed);
                    }
                    
                    if (!(this.transform.position.y - water.gameObject.transform.position.y > 0f) && splineDone)
                    {
                        this.transform.position = new Vector3(this.transform.position.x, water.transform.position.y, this.transform.position.z);
                        SwitchToBehaviourClientRpc((int)State.RESET);
                        SetPlayerGrabbedClientRpc(0, true);
                    }
                    break;
                case (int)State.RESET:
                    if (resetTimer >= 10)
                    {
                        resetEnemy();
                    }
                    break;
            }
        }
    }
}
