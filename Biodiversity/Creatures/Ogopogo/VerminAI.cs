using Biodiversity.General;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo
{
    internal class VerminAI : BiodiverseAI
    {
        public enum State
        {
            WANDERING,
            CHASING
        }

        // Variables related to water
        QuicksandTrigger water;
        QuicksandTrigger[] sandAndWater = GameObject.FindObjectsOfType<QuicksandTrigger>();
        List<QuicksandTrigger> waters = new List<QuicksandTrigger>();

        float damageTimer = 0f;

        // Wander vars
        float wanderTimer = 0f;
        Vector3 wanderPos = Vector3.zero;

        bool wallInFront = false;

        [SerializeField] private Transform RaycastPos;
        float speed = 5f;
        float loseRange = 15f;
        float detectionRange = 10f;

        [NonSerialized] public QuicksandTrigger setWater = null;
        [NonSerialized] public bool spawnedByOgo = false;

        public override void Start()
        {
            base.Start();
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

                if (waters.Count == 0 || enemyType.numberSpawned >= enemyType.MaxCount)
                {
                    BiodiversityPlugin.Logger.LogInfo("Despawning because there are too many of this enemy or there is no water. (vermin)");
                    RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(this.gameObject.GetComponent<NetworkObject>()));
                    return;
                }

                if (spawnedByOgo)
                {
                    RoundManager.Instance.SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
                    enemyType.numberSpawned++;
                }
                else if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Flooded)
                {
                    BiodiversityPlugin.Logger.LogInfo("Despawning because Ogopogo did not spawn this and it is not flooded. (vermin)");
                    SubtractFromPowerLevel();
                    RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(this.gameObject.GetComponent<NetworkObject>()));
                    return;
                }

                // Set the water he will stay in and teleport to it
                water = waters[UnityEngine.Random.Range(0, waters.Count)];
                if (setWater != null)
                {
                    water = setWater;
                }

                transform.position = water.transform.position; 
                setWanderPos();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void setWanderPos()
        {
            BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();
            wanderPos.x = UnityEngine.Random.Range(collider.bounds.min.x, collider.bounds.max.x);
            wanderPos.y = UnityEngine.Random.Range(collider.bounds.min.y, collider.bounds.max.y);
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

        // 2d distance formula
        float Distance2d(GameObject obj1, GameObject obj2)
        {
            return Mathf.Sqrt(Mathf.Pow(obj1.transform.position.x - obj2.transform.position.x, 2f) + Mathf.Pow(obj1.transform.position.z - obj2.transform.position.z, 2f));
        }

        bool Collision(Vector3 pos, BoxCollider col)
        {
            return col.bounds.Contains(pos);
        }

        void TurnTowardsLocation3d(Vector3 location)
        {
            this.transform.LookAt(new Vector3(location.x, location.y, location.z));
        }

        bool checkForWall()
        {
            return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 2f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
        }

        bool PlayerCheck(float range)
        {
            bool ret = false;
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                //BiodiversityPlugin.Logger.LogInfo(PlayerDistances[0]);
                if (Distance2d(player.gameObject, this.gameObject) < range)
                {
                    ret = true;
                }
            }
            return ret;
        }

        public override void Update()
        {
            base.Update();
            // Step timers
            if (currentBehaviourStateIndex == (int)State.WANDERING)
            {
                wanderTimer += Time.deltaTime;
            }
            if (damageTimer >= 0)
            {
                damageTimer -= Time.deltaTime;
            }

        }

        public void FixedUpdate()
        {
            wallInFront = checkForWall();
        }

        public override void OnCollideWithPlayer(UnityEngine.Collider other)
        {
            if (damageTimer <= 0)
            {
                other.gameObject.GetComponent<PlayerControllerB>().DamagePlayer(5, false, true, CauseOfDeath.Mauling, 0, false, default);
                damageTimer = 0.5f;
            }
        }

        float WaterTop(BoxCollider coll)
        {
            return coll.transform.localScale.y * coll.size.y / 2 + coll.transform.position.y;
        }


        public override void DoAIInterval()
        {
            base.DoAIInterval();

            BoxCollider coll = water.gameObject.GetComponent<BoxCollider>();

            if (WaterTop(coll) < transform.position.y)
            {
                transform.position = new Vector3(transform.position.x, WaterTop(coll), transform.position.z);
            }
            

            switch (currentBehaviourStateIndex)
            {
                case (int)State.WANDERING:
                    float step1 = speed * Time.deltaTime;
                    if (wanderTimer >= 5)
                    {
                        setWanderPos();
                    }

                    Vector3 WanderLocation = Vector3.MoveTowards(transform.position, wanderPos, step1);

                    TurnTowardsLocation3d(wanderPos);


                    if (wallInFront || !Collision(WanderLocation, coll))
                    {
                        BiodiversityPlugin.Logger.LogInfo("Found wall while wandering (vermin)");
                        setWanderPos();
                    }
                    else
                    {
                        transform.position = WanderLocation;
                    }

                    if (PlayerCheck(detectionRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.CHASING);
                    }
                    break;
                case (int)State.CHASING:
                    float step2 = speed * Time.deltaTime;
                    PlayerControllerB player = getClosestPlayer();

                    Vector3 newLocation = Vector3.MoveTowards(transform.position, player.gameObject.transform.position, step2);

                    BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();

                    TurnTowardsLocation3d(player.gameObject.transform.position);

                    if (Collision(newLocation, collider) && !wallInFront)
                    {
                        transform.position = newLocation;
                    }

                    if (!PlayerCheck(loseRange))
                    {
                        SwitchToBehaviourClientRpc((int)State.WANDERING);
                    }
                    break;
            }
        }
    }
}
