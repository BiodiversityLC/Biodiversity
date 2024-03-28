using Biodiversity.General;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;
public class HoneyFeederAI : BiodiverseAI {
    public enum AIStates {
        WANDERING, // wandering looking for hives
        FOUND_HIVE, // heading to hive
        ATTACKING,
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
                    if(Vector3.Distance(targetHive.transform.position, transform.position) <= Config.HiveDetectionDistance) {
                        State = AIStates.FOUND_HIVE; break;
                    }
                    targetHive = null;
                }

                foreach(GrabbableObject hive in possibleHives) {
                    Log($"distance to hive: {Vector3.Distance(hive.transform.position, transform.position)} <= {Config.HiveDetectionDistance}");
                    if(Vector3.Distance(hive.transform.position, transform.position) <= Config.HiveDetectionDistance) {
                        if(hive.playerHeldBy == null) {
                            targetHive = hive;
                            State = AIStates.FOUND_HIVE;
                        } else {
                            targetPlayer = hive.playerHeldBy;
                            State = AIStates.ATTACKING;
                        }
                        break;
                    }
                }
                break;
            case AIStates.FOUND_HIVE:
                destination = targetHive.transform.position;
                moveTowardsDestination = true;
                break;
            case AIStates.ATTACKING:
                movingTowardsTargetPlayer = true;

                break;
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false) {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
    }
}
