﻿using Biodiversity.General;
using Biodiversity.Util;
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
        [NonSerialized] public bool spawnedByVermin = false;

        // Mapping
        public Transform MapDot;

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
                    BiodiversityPlugin.Logger.LogInfo(maybeWater.gameObject.CompareTag("SpawnDenialPoint"));
                    if (maybeWater.isWater && !maybeWater.gameObject.CompareTag("SpawnDenialPoint"))
                    {
                        waters.Add(maybeWater);
                    }
                }

                if (waters.Count == 0 || enemyType.numberSpawned >= enemyType.MaxCount && !spawnedByVermin)
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

                if (spawnedByVermin)
                {
                    RoundManager.Instance.SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
                }

                if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Flooded && !spawnedByOgo)
                {
                    BiodiversityPlugin.Logger.LogInfo("Despawning because Ogopogo did not spawn this and it is not flooded. (vermin)");
                    SubtractFromPowerLevel();
                    RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(this.gameObject.GetComponent<NetworkObject>()));
                    return;
                }

                if (!spawnedByOgo && !spawnedByVermin)
                {
                    spawnVermin();
                }

                // Set the water he will stay in and teleport to it
                water = waters[UnityEngine.Random.Range(0, waters.Count)];
                if (setWater != null)
                {
                    water = setWater;
                }

                transform.position = water.transform.position; 
                setWanderPos();

                BoxCollider collider = water.gameObject.GetComponent<BoxCollider>();

                if (!spawnedByOgo)
                {
                    transform.position = new Vector3(transform.position.x, collider.bounds.max.y, transform.position.z);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void spawnVermin()
        {
            foreach (var i in Enumerable.Range(0, 3))
            {
                GameObject vermin = UnityEngine.Object.Instantiate<GameObject>(BiodiverseAssets.Vermin.enemyPrefab, this.transform.position, Quaternion.Euler(new Vector3(0, 0, 0)));
                vermin.GetComponentInChildren<NetworkObject>().Spawn(true);
                VerminAI AIscript = vermin.gameObject.GetComponent<VerminAI>();
                AIscript.setWater = water;
                AIscript.spawnedByVermin = true;
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

            if (StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory)
            {
                MapDot.position = this.transform.position;
            }
            else
            {
                MapDot.position = new Vector3(this.transform.position.x, StartOfRound.Instance.mapScreen.targetedPlayer.transform.position.y, this.transform.position.z);
            }

            if (isEnemyDead && transform.position.y < water.GetComponent<BoxCollider>().bounds.max.y)
            {
                Rise(0.2f);
            }

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
            if (isEnemyDead || stunNormalizedTimer > 0)
            {
                return;
            }
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

        public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
        {
            base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
            enemyHP -= force;
            if (enemyHP <= 0)
            {
                KillEnemyOnOwnerClient();
            }
        }

        void Rise(float speed)
        {
            this.transform.Translate(Vector3.up * speed * Time.deltaTime);
        }
        public override void DoAIInterval()
        {
            base.DoAIInterval();

            BoxCollider coll = water.gameObject.GetComponent<BoxCollider>();

            if (WaterTop(coll) < transform.position.y)
            {
                transform.position = new Vector3(transform.position.x, WaterTop(coll), transform.position.z);
            }

            if (stunNormalizedTimer > 0)
            {
                creatureAnimator.SetBool("Stun", true);
                return;
            }
            else
            {
                creatureAnimator.SetBool("Stun", false);
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