using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo;

internal class VerminAI : BiodiverseAI
{
    private enum State
    {
        Wandering,
        Chasing
    }

    // Variables related to water
    private QuicksandTrigger water;
    private BoxCollider waterCollider;
    private readonly QuicksandTrigger[] sandAndWater = FindObjectsOfType<QuicksandTrigger>();
    private readonly List<QuicksandTrigger> waters = [];

    private float damageTimer;
    private readonly NetworkVariable<bool> damageTimerBelowZero = new(); 

    // Wander vars
    private float wanderTimer;
    private Vector3 wanderPos = Vector3.zero;

    private bool wallInFront;

    [SerializeField] private Transform RaycastPos;
    private const float Speed = 5f;
    private const float LoseRange = 15f;
    private const float DetectionRange = 10f;

    [NonSerialized] public QuicksandTrigger SetWater;
    [NonSerialized] public Vector3 SetPos;

    [NonSerialized] public bool SpawnedByOgo = false;
    [NonSerialized] private bool spawnedByVermin;

    // Mapping
    public Transform MapDot;
    
    private static readonly int Stun = Animator.StringToHash("Stun");

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
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

            if (waters.Count == 0 || enemyType.numberSpawned >= enemyType.MaxCount && !spawnedByVermin)
            {
                // BiodiversityPlugin.Logger.LogInfo("Despawning because there are too many of this enemy or there is no water. (vermin)");
                RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(gameObject.GetComponent<NetworkObject>()));
                return;
            }

            if (SpawnedByOgo)
            {
                RoundManager.Instance.SpawnedEnemies.Add(gameObject.GetComponent<EnemyAI>());
                enemyType.numberSpawned++;
            }

            if (spawnedByVermin)
            {
                RoundManager.Instance.SpawnedEnemies.Add(this);
            }

            if (TimeOfDay.Instance.currentLevelWeather != LevelWeatherType.Flooded && !SpawnedByOgo)
            {
                // BiodiversityPlugin.Logger.LogInfo("Despawning because Ogopogo did not spawn this, and it is not flooded. (vermin)");
                SubtractFromPowerLevel();
                RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(gameObject.GetComponent<NetworkObject>()));
                return;
            }

            foreach (string levelName in OgopogoHandler.Instance.Config.VerminDisableLevels.Split(","))
            {
                if (levelName == StartOfRound.Instance.currentLevel.name && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Flooded)
                {
                    BiodiversityPlugin.Logger.LogInfo("Despawning Vermin because they are disabled during flooding on this moon.");
                    SubtractFromPowerLevel();
                    RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(gameObject.GetComponent<NetworkObject>()));
                    return;
                }
            }

            if (!SpawnedByOgo && !spawnedByVermin)
            {
                SpawnVermin();
            }

            // Set the water he will stay in and teleport to it
            water = waters[UnityEngine.Random.Range(0, waters.Count)];
            if (SetWater != null) water = SetWater;

            if (SetPos != null) { transform.position = SetPos; }
            else { transform.position = water.transform.position; }

            waterCollider = water.gameObject.GetComponent<BoxCollider>();

            SetWanderPos();


            if (!SpawnedByOgo)
                transform.position = new Vector3(transform.position.x, waterCollider.bounds.max.y, transform.position.z);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private void SpawnVermin()
    {
        if (!IsServer) return;
        foreach (int i in Enumerable.Range(0, 3))
        {
            GameObject vermin = Instantiate(OgopogoHandler.Instance.Assets.VerminEnemyType.enemyPrefab, transform.position, Quaternion.Euler(new Vector3(0, 0, 0)));
            vermin.GetComponentInChildren<NetworkObject>().Spawn(true);
            VerminAI aIscript = vermin.gameObject.GetComponent<VerminAI>();
            aIscript.SetWater = water;
            aIscript.spawnedByVermin = true;
        }
    }

    private void SetWanderPos()
    {
        wanderPos.x = UnityEngine.Random.Range(waterCollider.bounds.min.x, waterCollider.bounds.max.x);
        wanderPos.y = UnityEngine.Random.Range(waterCollider.bounds.min.y, waterCollider.bounds.max.y);
        wanderPos.z = UnityEngine.Random.Range(waterCollider.bounds.min.z, waterCollider.bounds.max.z);

        wanderTimer = 0f;
    }

    // Get the closest player in 2d space
    private PlayerControllerB GetClosestPlayer()
    {
        PlayerControllerB ret = null;
        float smallestDistance = 0f;
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
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
        return ret;
    }

    private static bool Collision(Vector3 pos, Collider col)
    {
        return col.bounds.Contains(pos);
    }

    private void TurnTowardsLocation3d(Vector3 location)
    {
        transform.LookAt(new Vector3(location.x, location.y, location.z));
    }

    private bool CheckForWall()
    {
        return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 2f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
    }

    private bool PlayerCheck(float range)
    {
        bool ret = false;
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            //BiodiversityPlugin.Logger.LogInfo(PlayerDistances[0]);
            if (Distance2d(player.gameObject, gameObject) < range)
            {
                ret = true;
            }
        }
        return ret;
    }

    public override void Update()
    {
        base.Update();

        MapDot.position = StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory ? transform.position : 
            new Vector3(transform.position.x, StartOfRound.Instance.mapScreen.targetedPlayer.transform.position.y, transform.position.z);

        if (!IsServer) return;
        if (isEnemyDead && transform.position.y < waterCollider.bounds.max.y && (IsHost || IsServer))
            Rise(0.2f);

        // Step timers
        if (currentBehaviourStateIndex == (int)State.Wandering)
        {
            wanderTimer += Time.deltaTime;
        }
        
        damageTimer -= Time.deltaTime;
        damageTimerBelowZero.Value = damageTimer <= 0;
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        wallInFront = CheckForWall();
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (isEnemyDead || stunNormalizedTimer > 0) return;
        
        if (damageTimerBelowZero.Value)
        {
            other.gameObject.GetComponent<PlayerControllerB>().DamagePlayer(5, false, true, CauseOfDeath.Mauling, 0, false, default);
            ResetDamageTimerServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ResetDamageTimerServerRpc()
    {
        damageTimer = 0.5f;
    }

    private static float WaterTop(BoxCollider coll)
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

    private void Rise(float riseSpeed)
    {
        transform.Translate(Vector3.up * (riseSpeed * Time.deltaTime));
    }
    
    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (IsServer && WaterTop(waterCollider) < transform.position.y)
            transform.position = new Vector3(transform.position.x, WaterTop(waterCollider), transform.position.z);

        if (stunNormalizedTimer > 0)
        {
            creatureAnimator.SetBool(Stun, true);
            return;
        }
        else
        {
            creatureAnimator.SetBool(Stun, false);
        }
        
        if (!IsServer) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)State.Wandering:
                float step1 = Speed * Time.deltaTime;
                if (wanderTimer >= 5)
                {
                    SetWanderPos();
                }

                Vector3 wanderLocation = Vector3.MoveTowards(transform.position, wanderPos, step1);

                TurnTowardsLocation3d(wanderPos);


                if (wallInFront || !Collision(wanderLocation, waterCollider))
                {
                    // BiodiversityPlugin.Logger.LogInfo("Found wall while wandering (vermin)");
                    SetWanderPos();
                }
                else
                {
                    transform.position = wanderLocation;
                }

                if (PlayerCheck(DetectionRange))
                {
                    SwitchToBehaviourClientRpc((int)State.Chasing);
                }
                break;
            case (int)State.Chasing:
                float step2 = Speed * Time.deltaTime;
                PlayerControllerB player = GetClosestPlayer();

                Vector3 newLocation = Vector3.MoveTowards(transform.position, player.gameObject.transform.position, step2);

                TurnTowardsLocation3d(player.gameObject.transform.position);

                if (Collision(newLocation, waterCollider) && !wallInFront)
                {
                    transform.position = newLocation;
                }

                if (!PlayerCheck(LoseRange))
                {
                    SwitchToBehaviourClientRpc((int)State.Wandering);
                }
                break;
        }
    }
}