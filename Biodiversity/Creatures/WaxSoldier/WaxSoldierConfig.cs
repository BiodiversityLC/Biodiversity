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
    
    [field: Tooltip("Whether the Wax Soldier will spawn in games.")]
    public bool WaxSoldierEnabled { get; private set; } = true;
    
    [field: Tooltip("Spawn weight of the Wax Soldier on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "havent filled this out yet";

    [field: Tooltip("The power level of the Wax Soldier.")]
    [field: Range(0f, 15f)]
    public float PowerLevel { get; private set; } = 1f;

    [field: Tooltip("The max amount of Wax Soldiers that can spawn in the map.")]
    [field: Range(0, 100)]
    public int MaxAmount { get; private set; } = 1;
    
    #endregion
    
    #region Advanced Settings
    [field: Header("Advanced Settings")]
    
    [field: Tooltip("How often (in seconds) the Wax Soldier updates its logic. Higher values increase performance but slow down reaction times.")]
    [field: Range(0.001f, 1f)]
    public float AiIntervalTime { get; private set; } = 0.03f;
    
    #endregion
}