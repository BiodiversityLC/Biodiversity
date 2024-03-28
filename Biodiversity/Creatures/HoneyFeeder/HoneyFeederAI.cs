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
        WANDERING, // wandering looking for hives
        FOUND_HIVE, // heading to hive
        ATTACKING_BACKINGUP,
        ATTACKING_CHARGING,
        RETURNING,
        DIGESTING
    }
    public enum DigestionStates {
        NONE,
        PARTLY
    }

    List<GrabbableObject> possibleHives;
    GrabbableObject targetHive;

    Transform nest;

    [field: SerializeField]
    public HoneyFeederConfig Config { get; private set; } = new HoneyFeederConfig();

    AIStates _state = AIStates.WANDERING;
    AIStates _prevState = AIStates.WANDERING;
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
        possibleHives = FindObjectsOfType<RedLocustBees>().Select(bees => bees.hive).ToList();
        Log("Possible hives count: " + possibleHives.Count);
    }

    void Log(string message) {
        BiodiversityPlugin.Logger.LogInfo($"[HoneyFeeder] " + message);
    }

    public override void DoAIInterval() { // biodiversity calculates everything host end, so this should always be run on the host.
        //if(!ShouldProcessEnemy()) return; // <- disabled for testing
        base.DoAIInterval();

        switch(State) {
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
                        if(hive.playerHeldBy == null) {
                            targetHive = hive;
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

                destination = targetHive.transform.position;
                moveTowardsDestination = true;
                break;
            case AIStates.ATTACKING_BACKINGUP:
                if(!moveTowardsDestination) {
                    destination = GetRandomPositionNearPlayer(targetPlayer, minDistance: Config.MinBackupAmount, radius: Config.MaxBackupAmount - Config.MinBackupAmount);
                    moveTowardsDestination = true;
                }
                if(HasFinishedAgentPath()) {
                    float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
                    if(distance > Config.SightDistance) {
                        // too far
                        State = AIStates.WANDERING; // wandering state will automatically fix if the targetHive is still in range.
                    } else if(distance < Config.TooCloseAmount) { // too close
                        destination = GetRandomPositionNearPlayer(targetPlayer, minDistance: Config.MinBackupAmount, radius: Config.MaxBackupAmount - Config.MinBackupAmount);
                        moveTowardsDestination = true;
                    } else { // perfect distance
                        State = AIStates.ATTACKING_CHARGING;
                    }
                }
                break;
            case AIStates.ATTACKING_CHARGING:
                agent.speed = Config.ChargeSpeed;
                movingTowardsTargetPlayer = true;
                
                break;
        }
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

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false) {
        base.HitEnemy(force, playerWhoHit, playHitSFX);

        targetPlayer = playerWhoHit;
        State = AIStates.ATTACKING_BACKINGUP;
    }
}
