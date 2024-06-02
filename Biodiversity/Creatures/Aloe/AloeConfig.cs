using System;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

[Serializable]
internal class AloeConfig(ConfigFile cfg) : BiodiverseConfig<AloeConfig>(cfg)
{
    [field: Header("General Settings")] 
    public float MaxRoamingRadius { get; private set; } = 50f;
    public float ViewWidth { get; private set; } = 135f;
    public int ViewRange { get; private set; } = 80;
    public int ProximityAwareness { get; private set; } = 3;
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    public int PlayerHealthThresholdForHealing { get; private set; } = 45;
}