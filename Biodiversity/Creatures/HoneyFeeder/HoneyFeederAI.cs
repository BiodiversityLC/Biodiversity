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

    AIStates _state = AIStates.WANDERING;
    AIStates _prevState = AIStates.WANDERING;
    DigestionStates digestion = DigestionStates.NONE;

    public AIStates State { 
        get { return _state; } 
        private set {
            Log($"Updating state: {_state} -> {value}");
            _prevState = _state;
            _state = value; 
        }
    }

    AISearchRoutine roamingRoutine = new();

    public override void Start() {
        base.Start();
        possibleHives = FindObjectsOfType<RedLocustBees>().Select(bees => bees.hive).ToList();
    }

    void Log(string message) {
        BiodiversityPlugin.Logger.LogInfo($"[HoneyFeeder] " + message);
    }

    public override void DoAIInterval() { // biodiversity calculates everything host end, so this should always be run on the host.
        //if(!ShouldProcessEnemy()) return;
        base.DoAIInterval();
        Log("DoAIInterval();");

        switch(State) {
            case AIStates.WANDERING:
                if(!roamingRoutine.inProgress) {
                    StartSearch(transform.position, roamingRoutine);
                }
                break;
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false) {
        base.HitEnemy(force, playerWhoHit, playHitSFX);
    }
}
