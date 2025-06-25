using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

[Serializable]
public class WaxSoldierConfig(ConfigFile cfg) : BiodiverseConfigLoader<WaxSoldierConfig>(cfg)
{
    #region Spawn Settings
    [field: Header("Spawn Settings")]
    
    [field: Tooltip("Whether the Wax Soldier will spawn in games. Dont turn this on or your game will implode.")]
    public bool WaxSoldierEnabled { get; private set; } = false;
    
    [field: Tooltip("Spawn weight of the Wax Soldier on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "All:1";

    [field: Tooltip("The power level of the Wax Soldier.")]
    [field: Range(0f, 15f)]
    public float PowerLevel { get; private set; } = 1f;

    [field: Tooltip("The max amount of Wax Soldiers that can spawn in the map.")]
    [field: Range(0, 100)]
    public int MaxAmount { get; private set; } = 1;
    
    #endregion
    
    #region Movement Settings
    [field: Header("Movement Settings")]
    
    [field: Tooltip("The max spead of the Wax Solder when he's on patrol.")]
    [field: Range(0.01f, 500f)]
    public float PatrolMaxSpeed { get; private set; } = 5f;
    
    [field: Tooltip("The max acceleration of the Wax Solder when he's on patrol.")]
    [field: Range(0.01f, 500f)]
    public float PatrolMaxAcceleration { get; private set; } = 8f;
    
    #endregion
    
    #region General Settings
    [field: Header("General Settings")]
    
    [field: Tooltip("The health of the Wax Soldier upon spawning.")]
    [field: Range(1, 100)]
    public int Health { get; private set; } = 8; // todo: check if this is the correct default health value
    
    [field: Tooltip("The speed multiplier for how quickly the Wax Soldier can open doors.")]
    [field: Range(0f, 100f)]
    public float OpenDoorSpeedMultiplier { get; private set; } = 3f;
    
    [field: Tooltip("The view width in degrees of the Wax Soldier.")]
    [field: Range(1f, 360f)]
    public float ViewWidth { get; private set; } = 115f;
    
    [field: Tooltip("The view range in meters of the Wax Soldier.")]
    [field: Range(0.1f, 200f)]
    public float ViewRange { get; private set; } = 65f;
    
    [field: Tooltip("Whether landmines and seamines (from the Surfaced mod) will blow up if the Wax Soldier moves over one.")]
    public bool LandminesBlowUpWaxSoldier { get; private set; } = true;
    // The wax soldier should be able to actively avoid landmines and traps in general.
    
    #endregion
    
    #region Advanced Settings
    [field: Header("Advanced Settings")]
    
    [field: Tooltip("How often (in seconds) the Wax Soldier updates its logic. Higher values increase performance but slow down reaction times.")]
    [field: Range(0.001f, 1f)]
    public float AiIntervalTime { get; private set; } = 0.03f;
    
    #endregion
}