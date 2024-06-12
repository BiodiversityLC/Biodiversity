using System;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

[Serializable]
public class AloeConfig(ConfigFile cfg) : BiodiverseConfigLoader<AloeConfig>(cfg)
{
    [field: Header("General Settings")] 
    
    [field: Tooltip("The maximum radius in meters the Aloe will roam from her favourite spot")]
    [field: Range(0f, 500f)]
    public float MaxRoamingRadius { get; private set; } = 25f;
    
    [field: Tooltip("The view width in degrees of the Aloe")]
    [field: Range(1f, 360f)]
    public float ViewWidth { get; private set; } = 135f;
    
    [field: Tooltip("The view range in meters of the Aloe")]
    [field: Range(1f, 200f)]
    public int ViewRange { get; private set; } = 80;
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to stalk them")]
    [field: Range(1f, 100f)]
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    
    [field: Tooltip("The required health a player needs to be or lower for the Aloe to kidnap and heal them")]
    [field: Range(1f, 100f)]
    public int PlayerHealthThresholdForHealing { get; private set; } = 45;

    [field: Tooltip("The distance from the player the Aloe will stop and stare at the player from")]
    [field: Range(0.5f, 100f)]
    public float PassiveStalkStaredownDistance { get; private set; } = 10f;
    
    [field: Tooltip("The time it takes for the Aloe to fully heal the player (from 1 to 100 health)")]
    [field: Range(1f, 120f)]
    public float TimeItTakesToFullyHealPlayer { get; private set; } = 15f;
    
    [field: Tooltip("The amount of 'escape charge' you get per spacebar press when trying to escape from the Aloe")]
    [field: Range(0.01f, 500f)]
    public float EscapeChargePerPress { get; private set; } = 15f;

    [field: Tooltip("The amount of 'escape charge' that decays per second when trying to escape from the Aloe")]
    [field: Range(0.01f, 100f)]
    public float EscapeChargeDecayRate { get; private set; } = 15;

    [field: Tooltip("The amount of 'escape charge' needed to break free from the Aloe")]
    [field: Range(1f, 1000f)]
    public float EscapeChargeThreshold { get; private set; } = 100f;

    [field: Tooltip("The amount of time it takes for the Aloe to transition to/from the dark material")]
    [field: Range(0f, 30f)]
    public float DarkSkinTransitionTime { get; private set; } = 7.5f;
    
    [field: Tooltip("TEMPORARY SETTING, WILL BE REMOVED LATER.")]
    public int Rarity { get; private set; } = 100;
}