using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

[Serializable]
public class WaxSoliderConfig(ConfigFile cfg) : BiodiverseConfigLoader<WaxSoliderConfig>(cfg)
{
    [field: Header("Spawn Settings.")]
    [field: Tooltip("Whether the Wax Soldier will spawn in games.")]
    public bool WaxSoldierEnabled { get; private set; } = true;
    
    [field: Tooltip("Spawn weight of the Wax Soldier on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "";

    [field: Tooltip("The power level of the Wax Soldier.")]
    [field: Range(0f, 15f)]
    public float PowerLevel { get; private set; } = 1f;

    [field: Tooltip("The max amount of Wax Soldier that can spawn in the map.")]
    [field: Range(0, 10)]
    public int MaxAmount { get; private set; } = 1;
    
    [field: Header("General Settings.")]
    [field: Tooltip("The health of the Wax Soldier upon spawning.")]
    [field: Range(1, 100)]
    public int Health { get; private set; } = 6;
}