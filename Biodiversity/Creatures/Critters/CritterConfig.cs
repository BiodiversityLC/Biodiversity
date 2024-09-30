using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

[Serializable]
public class CritterConfig(ConfigFile configFile) : BiodiverseConfigLoader<CritterConfig>(configFile) 
{
    [field: Header("Prototax")]
	
    [field: Tooltip("Whether the Prototax will spawn in games.")] 
    public bool PrototaxEnabled { get; private set; } = true;
	
    [field: Tooltip("Spawn weight of the Prototax on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string PrototaxRarity { get; private set; } = "Experimentation:80,Adamance:15,March:45,Artifice:95,Solace:80,Fray:32,Seichi:16,Hydro:38,Collateral:8,Corrosion:5,Icebound:20,USC Vortex:8";

    [field: Tooltip("Prototax power level")] 
    [field: Range(0f, 5f)] 
    public float PrototaxPowerLevel { get; private set; } = 0.5f;

    [field: Tooltip("Max amount of Prototaxes that can spawn in a game.")]
    [field: Range(0, 20)]
    public int PrototaxMaxAmount { get; private set; } = 2;
	
    [field: Tooltip("Normal speed of the Prototax.")]
    [field: Range(0.1f, 50f)]
    public float PrototaxNormalSpeed { get; private set; } = 3.5f;
	
    [field: Tooltip("Normal acceleration of the Prototax.")]
    [field: Range(0.1f, 200f)]
    public float PrototaxNormalAcceleration { get; private set; } = 5f;

    [field: Tooltip("Speed of the Prototax after being hit.")]
    [field: Range(0.1f, 50f)]
    public float PrototaxBoostedSpeed { get; private set; } = 6f;
	
    [field: Tooltip("Acceleration of the Prototax after being hit.")]
    [field: Range(0.1f, 200f)]
    public float PrototaxBoostedAcceleration { get; private set; } = 7f;
	
    [field: Tooltip("Length of the speed boost after being hit.")]
    [field: Range(3f, 20f)]
    public float PrototaxBoostTime { get; private set; } = 6f;
	
    [field: Tooltip("Amount of time the Prototax spews the spores for after being hit.")]
    [field: Range(3f, 20f)]
    public float PrototaxSpewTime { get; private set; } = 3f;

    [field: Tooltip("When enabled, the Prototax will roam around the area that they spawned in, and not venture out too far.")]
    public bool PrototaxAnchoredWandering { get; private set; } = false;

    [field: Tooltip("The minimum time that the Prototax will wander for.")]
    [field: Range(5f, 1000f)]
    public float PrototaxWanderTimeMin { get; private set; } = 30f;

    [field: Tooltip("The maximum time that the Prototax will wander for.")]
    [field: Range(6f, 1001f)]
    public float PrototaxWanderTimeMax { get; private set; } = 240f;
	
    [field: Tooltip("The minimum time that the Prototax will idle for.")]
    [field: Range(1f, 120f)]
    public float PrototaxIdleTimeMin { get; private set; } = 15f;

    [field: Tooltip("The maximum time that the Prototax will idle for.")]
    [field: Range(2f, 121f)]
    public float PrototaxIdleTimeMax { get; private set; } = 35f;
	
    [field: Header("Leaf Boy")]
	
    [field: Tooltip("Whether the Leaf Boy will spawn in games.")]
    public bool LeafBoyEnabled { get; private set; } = true;
	
    [field: Tooltip("Spawn weight of the Leaf boys on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
    public string LeafBoyRarity { get; private set; } = "Vow:100,Adamance:15,Experimentation:0,Assurance:85,Offense:30,Artifice:0,Rend:0,Dine:0,Titan:0,Seichi:28,Fray:300,Vertigo:12,Integrity:25,Collateral:10,Hydro:38,USC Vortex:12";

    [field: Tooltip("LeafBoy power level")] 
    [field: Range(0f, 5f)] 
    public float LeafBoyPowerLevel { get; private set; } = 0.5f;

    [field: Tooltip("Max amount of LeafBoys that can spawn in a game.")]
    [field: Range(0, 20)]
    public int LeafBoyMaxAmount { get; private set; } = 2;
    
    [field: Tooltip("Normal speed of a LeafBoy.")]
    [field: Range(0.1f, 50f)]
    public float LeafBoyNormalSpeed { get; private set; } = 1.5f;
	
    [field: Tooltip("Normal acceleration of a LeafBoy.")]
    [field: Range(0.1f, 200f)]
    public float LeafBoyNormalAcceleration { get; private set; } = 5f;
	
    [field: Tooltip("Speed of a LeafBoy when they are scared.")]
    [field: Range(0.1f, 50f)]
    public float LeafBoyScaredSpeed { get; private set; } = 6f;
	
    [field: Tooltip("Acceleration of a LeafBoy when they are scared.")]
    [field: Range(0.1f, 200f)]
    public float LeafBoyScaredAcceleration { get; private set; } = 8f;
	
    [field: Tooltip("The distance a player has to be from a LeafBoy for him to get scared.")]
    [field: Range(1f, 20f)]
    public float LeafBoyScaryPlayerDistance { get; private set; } = 6f;

    [field: Tooltip("The time it takes for a LeafBoy to calm down (stop being scared) when left alone.")]
    [field: Range(1f, 120f)]
    public float LeafBoyPlayerForgetTime { get; private set; } = 3f;

    [field: Tooltip("How many LeafBoys there can be in a group")] 
    [field: Range(1, 1000)]
    public int LeafBoyGroupSize { get; private set; } = 8;
}