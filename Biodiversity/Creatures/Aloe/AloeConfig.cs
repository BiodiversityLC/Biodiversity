using System;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

[Serializable]
public class AloeConfig(ConfigFile cfg) : BiodiverseConfigLoader<AloeConfig>(cfg)
{
    [field: Header("General Settings.")]
    [field: Tooltip("The health of the Aloe upon spawning.")]
    [field: Range(1, 100)]
    public int Health { get; private set; } = 6;

    [field: Tooltip("The damage that the Aloe's slap does to players.")]
    [field: Range(0, 500)]
    public int SlapDamagePlayers { get; private set; } = 45;
    
    [field: Tooltip("The damage that the Aloe's slap does to enemies.")]
    [field: Range(0, 10)]
    public int SlapDamageEnemies { get; private set; } = 2;
    
    [field: Tooltip("The radius in meters the Aloe is allowed roam from her favourite spot.")]
    [field: Range(45f, 500f)]
    public float RoamingRadius { get; private set; } = 50f;
    
    [field: Tooltip("The view width in degrees of the Aloe.")]
    [field: Range(1f, 360f)]
    public float ViewWidth { get; private set; } = 135f;
    
    [field: Tooltip("The view range in meters of the Aloe.")]
    [field: Range(1, 200)]
    public int ViewRange { get; private set; } = 80;
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to stalk them.")]
    [field: Range(1, 100)]
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to kidnap and heal them.")]
    [field: Range(1, 100)]
    public int PlayerHealthThresholdForHealing { get; private set; } = 45;

    [field: Tooltip("The distance from the player the Aloe will stop and stare at the player from.")]
    [field: Range(0.5f, 100f)]
    public float PassiveStalkStaredownDistance { get; private set; } = 10f;
    
    [field: Tooltip("The time it takes for the Aloe to fully heal the player (from 1 to 100 health).")]
    [field: Range(1f, 120f)]
    public float TimeItTakesToFullyHealPlayer { get; private set; } = 15f;
    
    [field: Tooltip("The amount of 'escape charge' you get per spacebar press when trying to escape from the Aloe.")]
    [field: Range(0.01f, 500f)]
    public float EscapeChargePerPress { get; private set; } = 15f;

    [field: Tooltip("The amount of 'escape charge' that decays per second when trying to escape from the Aloe.")]
    [field: Range(0.01f, 100f)]
    public float EscapeChargeDecayRate { get; private set; } = 15f;

    [field: Tooltip("The amount of 'escape charge' needed to break free from the Aloe.")]
    [field: Range(1f, 1000f)]
    public float EscapeChargeThreshold { get; private set; } = 100f;

    [field: Tooltip("The amount of time it takes for the Aloe to transition to/from the dark material.")]
    [field: Range(0f, 30f)]
    public float DarkSkinTransitionTime { get; private set; } = 7.5f;

    [field: Tooltip("The amount of time the Aloe will stare at you before chasing you if you escape.")]
    [field: Range(0f, 30f)]
    public float WaitBeforeChasingEscapedPlayerTime { get; private set; } = 2f;

    // [field: Tooltip("Whether landmines will blow up if the Aloe moves over one while carrying a player.")]
    // public bool LandminesBlowUpAloe { get; private set; } = false;
    
    [field: Header("Spawn Settings.")]

    [field: Tooltip("Whether the Aloe will spawn in games.")]
    public bool AloeEnabled { get; private set; } = true;
    
    [field: Tooltip("Spawn weight of the Aloe on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "Experimentation:38,Assurance:75,March:85,Artifice:95,Aquatis:18,Vertigo:76,Solace:12,Azure:40,Argent:15,Solarius:10,Phuket:20,Sierra:40,Fray:45,Fission-C:5,Atlantica:5,Etern:12,Gloom:17,Junic:31,Polarus:13,Seichi:8,Modded:2";

    [field: Tooltip("The power level of the Aloe.")]
    [field: Range(0f, 15f)]
    public float PowerLevel { get; private set; } = 3f;

    [field: Tooltip("The max amount of Aloes that can spawn in the map.")]
    [field: Range(0, 10)]
    public int MaxAmount { get; private set; } = 1;
}