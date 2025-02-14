using System;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

[Serializable]
public class AloeConfig(ConfigFile cfg) : BiodiverseConfigLoader<AloeConfig>(cfg)
{
    [field: Header("Spawn Settings")]
    [field: Tooltip("Whether the Aloe will spawn in games.")]
    public bool AloeEnabled { get; private set; } = true;
    
    [field: Tooltip("Spawn weight of the Aloe on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string Rarity { get; private set; } = "Experimentation:28,Assurance:75,Offense:65,March:55,Artifice:95,Aquatis:18,Integrity:21,Vertigo:36,Solarius:10,Phuket:20,Sierra:40,Fray:45,Fission-C:5,Etern:12,Gloom:17,Junic:31,Polarus:13,Seichi:14,USCVortex:8,Hydro:20,Arcadia:18,Derelect:30,Arelion:7,Phaedra:50,Pelagia:18,Torus:26,Starship-13:35,Modded:2";

    [field: Tooltip("The power level of the Aloe.")]
    [field: Range(0f, 15f)]
    public float PowerLevel { get; private set; } = 1f;

    [field: Tooltip("The max amount of Aloes that can spawn in the map.")]
    [field: Range(0, 100)]
    public int MaxAmount { get; private set; } = 1;
    
    [field: Header("Movement Settings")]
    [field: Tooltip("The max speed of the Aloe when she's roaming.")]
    [field: Range(0.01f, 500f)]
    public float RoamingMaxSpeed { get; private set; } = 2f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's roaming.")]
    [field: Range(0.01f, 500f)]
    public float RoamingMaxAcceleration { get; private set; } = 2f;
    
    [field: Tooltip("The max speed of the Aloe when she's running away from a player.")]
    [field: Range(0.01f, 500f)]
    public float AvoidingPlayerMaxSpeed { get; private set; } = 9f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's running away from a player.")]
    [field: Range(0.01f, 500f)]
    public float AvoidingPlayerMaxAcceleration { get; private set; } = 50f;
    
    [field: Tooltip("The max speed of the Aloe when she's stalking a player.")]
    [field: Range(0.01f, 500f)]
    public float StalkingMaxSpeed { get; private set; } = 5f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's stalking a player.")]
    [field: Range(0.01f, 500f)]
    public float StalkingMaxAcceleration { get; private set; } = 50f;
    
    [field: Tooltip("The max speed of the Aloe when she's dragging a player on the floor.")]
    [field: Range(0.01f, 500f)]
    public float KidnappingPlayerDraggingMaxSpeed { get; private set; } = 6f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's dragging a player on the floor.")]
    [field: Range(0.01f, 500f)]
    public float KidnappingPlayerDraggingMaxAcceleration { get; private set; } = 8f;
    
    [field: Tooltip("The max speed of the Aloe when she's carrying a player.")]
    [field: Range(0.01f, 500f)]
    public float KidnappingPlayerCarryingMaxSpeed { get; private set; } = 10f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's carrying a player.")]
    [field: Range(0.01f, 500f)]
    public float KidnappingPlayerCarryingMaxAcceleration { get; private set; } = 20f;
    
    [field: Tooltip("The max speed of the Aloe when she's chasing an escaped player.")]
    [field: Range(0.01f, 500f)]
    public float ChasingEscapedPlayerMaxSpeed { get; private set; } = 6f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's chasing an escaped player.")]
    [field: Range(0.01f, 500f)]
    public float ChasingEscapedPlayerMaxAcceleration { get; private set; } = 12f;
    
    [field: Tooltip("The max speed of the Aloe when she's attacking a player (with intent on killing them).")]
    [field: Range(0.01f, 500f)]
    public float AttackingPlayerMaxSpeed { get; private set; } = 5f;
    
    [field: Tooltip("The max acceleration of the Aloe when she's attacking a player (with intent on killing them).")]
    [field: Range(0.01f, 500f)]
    public float AttackingPlayerMaxAcceleration { get; private set; } = 50f;

    [field: Tooltip("The maximum turning speed in (deg/s) while following a path. This setting is for all behaviour states of the Aloe.")]
    [field: Range(0.01f, 500f)]
    public float AngularSpeed { get; private set; } = 220f;
    
    [field: Tooltip("Whether the Aloe will try to avoid overshooting the destination point by slowing down in time. I suggest you leave this on.")]
    public bool AutoBraking { get; private set; } = true;
    
    [field: Header("General Settings")]
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
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to stalk them.")]
    [field: Range(1, 100)]
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to kidnap and heal them.")]
    [field: Range(1, 100)]
    public int PlayerHealthThresholdForHealing { get; private set; } = 60;

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

    [field: Tooltip("Whether landmines and seamines will blow up if the Aloe moves over one while carrying a player.")]
    public bool LandminesBlowUpAloe { get; private set; } = false;
    
    [field: Tooltip("How often (in seconds) the Aloe updates its logic. Higher values increase performance but slow down reaction times.")]
    [field: Range(0.001f, 1f)]
    public float AiIntervalTime { get; private set; } = 0.03f;
}
