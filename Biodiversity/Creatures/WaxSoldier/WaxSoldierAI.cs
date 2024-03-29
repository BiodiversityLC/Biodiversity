using Biodiversity.General;
using Biodiversity.Util;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GameNetcodeStuff;
using System.Linq;
using Unity.Netcode;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAI : BiodiverseAI
{
    public enum AIStates
    {
        STATIONARY, // Remain stationary and wait for a player to pass by
        PURSUING, // Shoot musket once (max cap of 1), then chase player in attempt to stab them (will only target this player throughout all phases until reset)
        SEARCHING, // Searching for player after being out of sight and range (Should this state exist or go straight to RETURNING ?)
        RETURNING, // Return to guardLocation after having lost the player for x amount of time
        RELOADING  // Have another state after being returned for the soldier to reload etc?
    }

    public enum MoltenStates
    {
        NORMAL,
        MOLTEN //When exposed to strong heat, melt into damaged/molten form (What would strong heat be?)
    }

    private Transform guardLocation;

    private AIStates _state = AIStates.STATIONARY;
    private AIStates _prevState = AIStates.STATIONARY;
    private MoltenStates molten = MoltenStates.NORMAL;

    public AIStates State
    {
        get { return _state; }
        private set
        {
            Log($"Updating state: {_state} -> {value}");
            moveTowardsDestination = false;
            movingTowardsTargetPlayer = false;
            //agent.speed = Config.NormalSpeed; //commented out because i have to edit BiodiversityPlugin.cs but i'm not sure how to handle the config
            // or should all creatures' configs work like this → internal static CreatureConfig.cs config; (Example)
            if (currentSearch.inProgress) StopSearch(currentSearch, true);

            _prevState = _state;
            _state = value;
        }
    }

    void Log(string message)
    {
        BiodiversityPlugin.Logger.LogInfo($"[WaxSoldier] " + message);
    }
}