using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

[Serializable]
public class CritterConfig(ConfigFile configFile) : BiodiverseConfigLoader<CritterConfig>(configFile) 
{
	[field: Header("Fungi")]
	
	[field: Tooltip("Whether the Fungi will spawn in games.")]
	public bool FungiEnabled { get; private set; } = true;
	
	[field: Tooltip("Spawn weight of the Fungi on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string FungiRarity { get; private set; } = "Experimentation:80,Adamance:8,March:45,Artifice:95,Solace:80,Fray:32,Seichi:16,Hydro:38,Collateral:8,Corrosion:5,Icebound:20,USC Vortex:8,Mycorditum:34";
	
	[field: Tooltip("Normal speed of fungi.")]
	[field: Range(3f, 20f)]
	public float FungiNormalSpeed { get; private set; } = 3.5f;

	[field: Tooltip("Speed of fungi after being hit.")]
	[field: Range(3f, 20f)]
	public float FungiBoostedSpeed { get; private set; } = 6f;
	
	[field: Tooltip("Length of boosted speed after being hit.")]
	[field: Range(3f, 20f)]
	public float FungiBoostTime { get; private set; } = 6f;
	
	[field: Tooltip("Length of stunned time after being hit.")]
	[field: Range(3f, 20f)]
	public float FungiStunTime { get; private set; } = 3f;
	
	[field: Header("Leaf Boy")]
	
	[field: Tooltip("Whether the Leaf Boy will spawn in games.")]
	public bool LeafBoyEnabled { get; private set; } = true;
	
	[field: Tooltip("Spawn weight of the Leaf boys on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string LeafBoyRarity { get; private set; } = "Vow:100,Adamance:15,Experimentation:0,Assurance:85,Offense:30,Artifice:0,Rend:0,Dine:0,Titan:0,Seichi:28,Fray:300,Vertigo:12,Integrity:25,Collateral:10,Hydro:38,USC Vortex:12";

	[field: Tooltip("The distance a player has to be from a LeafBoy for him to get scared.")]
	[field: Range(1f, 20f)] 
	public float LeafBoyScaryPlayerDistance { get; private set; } = 6f;

	[field: Tooltip("The normal speed of a LeafBoy.")]
	[field: Range(0.5f, 20f)]
	public float LeafBoyBaseMovementSpeed { get; private set; } = 1.5f;

	[field: Tooltip("The speed of a LeafBoy when they are scared.")]
	[field: Range(1f, 20f)]
	public float LeafBoyScaredSpeedMultiplier { get; private set; } = 4f;

	[field: Tooltip("The time it takes for a LeafBoy to calm down (stop being scared) when left alone.")]
	[field: Range(1f, 120f)]
	public float LeafBoyPlayerForgetTime { get; private set; } = 3f;
}
