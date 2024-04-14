using Biodiversity.General;
using Biodiversity.Util;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;
public class HoneyFeederAI : BiodiverseAI {
    public enum AIStates {
        ASLEEP, // starts asleep until 1pm
        WANDERING, // wandering looking for hives
        FOUND_HIVE, // heading to hive
        ATTACKING_BACKINGUP,
        ATTACKING_CHARGING,
        ATTACKING_SPITTING,
        RETURNING,
        DIGESTING
    }
    public enum DigestionStates {
        NONE,
        PARTLY
    }

    List<GrabbableObject> possibleHives;
    GrabbableObject targetHive;

    HoneyFeederNest nest;
    static HoneyFeederAI Instance;

    [field: SerializeField]
    public HoneyFeederConfig Config { get; private set; } = HoneyFeederHandler.Instance.Config;

    AIStates _state = AIStates.ASLEEP;
    AIStates _prevState = AIStates.ASLEEP;
    DigestionStates digestion = DigestionStates.NONE;


    public AIStates State { 
        get { return _state; } 
        private set {
            Log($"Updating state: {_state} -> {value}");
            moveTowardsDestination = false;
            movingTowardsTargetPlayer = false;
            agent.speed = Config.NormalSpeed;
            if(currentSearch.inProgress) StopSearch(currentSearch, true);

            _prevState = _state;
            _state = value; 
        }
    }

    AISearchRoutine roamingRoutine = new();

    public override void Start() {
        base.Start();

        if(Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        RefreshCollectableHives();
        // FIXME: Replace with nest prefab when we have modle
        nest = new GameObject("HoneyFeeder Nest").AddComponent<HoneyFeederNest>();
        Log("Possible hives count: " + possibleHives.Count);
    }

    void OnDisable() {
        if(Instance == this) Instance = null;
    }

    void Log(string message) {
        BiodiversityPlugin.Logger.LogInfo($"[HoneyFeeder] " + message);
    }

    public override void DoAIInterval() { // biodiversity calculates everything host end, so this should always be run on the host.
        //if(!ShouldProcessEnemy()) return; // <- disabled for testing
        base.DoAIInterval();

        switch(State) {
            case AIStates.ASLEEP:
                if(TimeOfDay.Instance.HasPassedTime(TimeOfDay.Instance.ParseTimeString(Config.WakeUpTime))) {
                    Log("Honeyfeeder is waking up!");
                    State = AIStates.WANDERING;
                }
                break;
            case AIStates.WANDERING:
                if(!roamingRoutine.inProgress) StartSearch(transform.position, roamingRoutine);

                if(targetHive != null) { // reset incase player successfully runs away with the hive.
                    if(Vector3.Distance(targetHive.transform.position, transform.position) <= Config.SightDistance) {
                        State = AIStates.FOUND_HIVE; break;
                    }
                    targetHive = null;
                }

                foreach(GrabbableObject hive in possibleHives) {
                    if(Vector3.Distance(hive.transform.position, transform.position) <= Config.SightDistance) {
                        targetHive = hive;
                        if(hive.playerHeldBy == null) {
                            State = AIStates.FOUND_HIVE;
                        } else {
                            targetPlayer = hive.playerHeldBy;
                            State = AIStates.ATTACKING_BACKINGUP;
                        }
                        break;
                    }
                }
                break;
            case AIStates.FOUND_HIVE:
                if(targetHive.playerHeldBy != null) {
                    targetPlayer = targetHive.playerHeldBy;
                    State = AIStates.ATTACKING_BACKINGUP;
                    break;
                }

                Log($"Distance to targetHive: {Vector3.Distance(transform.position, targetHive.transform.position)}");
                if(Vector3.Distance(transform.position, targetHive.transform.position) < 3.5f) {
                    // todo: have animation and wait for animation to finish.

                    GrabItem(targetHive);
                    GrabItemClientRPC(targetHive.NetworkObject);

                    destination = nest.transform.position;
                    moveTowardsDestination = true;
                    State = AIStates.RETURNING;
                } else {
                    destination = targetHive.transform.position;
                    moveTowardsDestination = true;
                }
                
                break;
            case AIStates.ATTACKING_BACKINGUP:
                if(!moveTowardsDestination) {
                    StartBackingUp();
                }
                if(targetPlayer.isPlayerDead) {
                    State = AIStates.WANDERING;
                    break;
                }
                if(HasFinishedAgentPath()) {
                    float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
                    if(distance > Config.SightDistance) {
                        // too far
                        State = AIStates.WANDERING; // wandering state will automatically fix if the targetHive is still in range.
                    } else if(distance < Config.TooCloseAmount) { // too close
                        StartBackingUp();
                    } else { // perfect distance
                        State = AIStates.ATTACKING_CHARGING;
                        destination = targetPlayer.transform.position + (transform.position.Direction(targetPlayer.transform.position) * Config.FollowthroughAmount);
                        moveTowardsDestination = true;
                    }
                }
                break;
            case AIStates.ATTACKING_CHARGING:
                agent.speed = Config.ChargeSpeed;
                moveTowardsDestination = true;

                if(HasFinishedAgentPath()) {
                    State = AIStates.ATTACKING_BACKINGUP;
                }

                break;
            case AIStates.RETURNING:
                destination = nest.transform.position;
                moveTowardsDestination = true;

                if(HasFinishedAgentPath()) {
                    State = AIStates.DIGESTING;
                }

                break;
            case AIStates.DIGESTING:
                if(targetHive.isHeldByEnemy) {
                    DropItem(targetHive);
                    DropItemClientRPC(targetHive.NetworkObject);
                }

                Log($"Current time: {TimeOfDay.Instance.GetCurrentTime()}. Digested time: {TimeOfDay.Instance.ParseTimeString(Config.TimeWhenPartlyDigested)}");

                if(targetHive.playerHeldBy != null) {
                    targetPlayer = targetHive.playerHeldBy;
                    State = AIStates.ATTACKING_BACKINGUP;
                }

                if(TimeOfDay.Instance.HasPassedTime(TimeOfDay.Instance.ParseTimeString(Config.TimeWhenPartlyDigested)) && digestion != DigestionStates.PARTLY) {
                    digestion = DigestionStates.PARTLY;
                    Log("Set digestion status to Partly.");

                    // play sound?
                    targetHive.scrapValue = Mathf.RoundToInt(targetHive.scrapValue * Config.PartlyDigestedScrapMultiplier);
                }

                break;
            default:
                BiodiversityPlugin.Logger.LogError($"[HoneyFeeder] {State} isn't valid?!??!??!?! NOT GOOD!!");
                break;
        }
    }

    // this is cooked :sob::sob:
    void GrabItem(GrabbableObject item) {
        item.parentObject = transform; // change to hold in hands i think??
        item.hasHitGround = false;
        item.isHeldByEnemy = true;
        item.GrabItemFromEnemy(this);
        item.EnablePhysics(false);
    }

    void DropItem(GrabbableObject item) {
        item.parentObject = null;
        item.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        item.EnablePhysics(true);
        item.fallTime = 0f;
        item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
        item.targetFloorPosition = item.transform.parent.InverseTransformPoint(RoundManager.Instance.RandomlyOffsetPosition(targetHive.GetItemFloorPosition(default), 1.2f, 0.4f));
        item.floorYRot = -1;
        item.isHeldByEnemy = false;
        item.DiscardItemFromEnemy();
    }

    [ClientRpc]
    void DropItemClientRPC(NetworkObjectReference itemRef) {
        if(IsOwner) return;
        if(itemRef.TryGet(out var networkObject)) {
            DropItem(networkObject.GetComponent<GrabbableObject>());
        }
    }


    [ClientRpc]
    void GrabItemClientRPC(NetworkObjectReference itemRef) {
        if(IsOwner) return;
        if(itemRef.TryGet(out var networkObject)) {
            GrabItem(networkObject.GetComponent<GrabbableObject>());
        }
    }

    void RefreshCollectableHives() {
        possibleHives = FindObjectsOfType<RedLocustBees>().Select(bees => bees.hive).ToList();
    }

    void StartBackingUp() {
        float radius = Config.MinBackupAmount + ((Config.MaxBackupAmount - Config.MinBackupAmount) * UnityEngine.Random.Range(0f, 1f));
        Vector3 directionFromPlayer = targetPlayer.transform.position.Direction(transform.position);
        Vector3 backupOrigin = targetPlayer.transform.position + (directionFromPlayer * radius);
        destination = GetRandomPositionOnNavMesh(backupOrigin, 2) + transform.position;
        moveTowardsDestination = true;
    }

    public override void OnCollideWithPlayer(Collider other) {
        base.OnCollideWithPlayer(other);
        if(!IsHost) return;

        if(State != AIStates.ATTACKING_CHARGING) return;
        if(other.TryGetComponent(out PlayerControllerB player)) {
            HitPlayerClientRpc((int)player.playerClientId);
            updateDestinationInterval = Config.StunTimeAfterHit;

            State = AIStates.ATTACKING_BACKINGUP;
        }
    }

    [ClientRpc]
    void HitPlayerClientRpc(int playerId) {
        PlayerControllerB hitPlayer = PlayerUtil.GetPlayerFromClientId(playerId);

        if(hitPlayer == GameNetworkManager.Instance.localPlayerController) {
            hitPlayer.DamagePlayer(Config.ChargeDamage, causeOfDeath: CauseOfDeath.Mauling);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1) {
        base.HitEnemy(force, playerWhoHit, playHitSFX);

        targetPlayer = playerWhoHit;
        State = AIStates.ATTACKING_BACKINGUP;
    }
}
