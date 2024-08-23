using System.Collections.Generic;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using LethalLib.Modules;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

public class CritterConfig(ConfigFile configFile) : BiodiverseConfigLoader<CritterConfig>(configFile) {
	[field: Header("Fungi")]
	[field: Tooltip("Normal speed of fungi.")]
	[field: Range(3, 20)]
	public float FungiNormalSpeed { get; private set; } = 3.5f;

	[field: Tooltip("Speed of fungi after being hit")]
	[field: Range(3, 20)]
	public float FungiBoostedSpeed { get; private set; } = 4f;
	
	[field: Tooltip("Length of boosted speed after being hit.")]
	[field: Range(3, 20)]
	public float FungiBoostTime { get; private set; } = 5f;
	
	[field: Tooltip("Length of stunned time after being hit.")]
	[field: Range(3, 20)]
	public float FungiStunTime { get; private set; } = 4f;

	[field: Tooltip("Whether the Fungi will spawn in games.")]
	public bool FungiEnabled { get; private set; } = true;
	
	// public EnemyRaritiesPerMoon FungiRarity { get; private set; } = new(
	// 	0,
	// 	new Dictionary<Levels.LevelTypes, int> {
	// 		{Levels.LevelTypes.ExperimentationLevel, 80},
	// 		{Levels.LevelTypes.AdamanceLevel, 100},
	// 		{Levels.LevelTypes.MarchLevel, 80},
	// 		{Levels.LevelTypes.ArtificeLevel, 100}
	// 	},
	// 	new Dictionary<string, int> {
	// 		{"Solace", 80},
	// 		{"Fray", 40}
	// 	}
	// );
	
	[field: Tooltip("Spawn weight of the Fungi on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string FungiRarity { get; private set; } = "Experimentation:80,Adamance:100,March:80,Artifice:100,Solace:80,Fray:32,Seichi:16,Hydro:38,Collateral:8,Corrosion:5,Icebound:20,USC Vortex:8";

	[field: Header("Leaf Boy")]
	[field: Tooltip("Whether the Leaf Boy will spawn in games.")]
	public bool LeafBoyEnabled { get; private set; } = true;
	
	// public EnemyRaritiesPerMoon LeafBoyRarity { get; private set; } = new(
	// 	0,
	// 	new Dictionary<Levels.LevelTypes, int> {
	// 		{Levels.LevelTypes.VowLevel, 65},
	// 		{Levels.LevelTypes.AdamanceLevel, 100},
	// 		{ Levels.LevelTypes.ExperimentationLevel, 70 },
	// 		{ Levels.LevelTypes.AssuranceLevel, 100 },
	// 		{ Levels.LevelTypes.OffenseLevel, 80 },
	// 		{ Levels.LevelTypes.ArtificeLevel, 65 },
	// 		{ Levels.LevelTypes.RendLevel, 50 },
	// 		{ Levels.LevelTypes.DineLevel, 65 },
	// 		{ Levels.LevelTypes.TitanLevel, 10 }
	// 	}
	// );
	
	[field: Tooltip("Spawn weight of the Fungi on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string LeafBoyRarity { get; private set; } = "Vow:100,Adamance:60,Experimentation:0,Assurance:85,Offense:30,Artifice:0,Rend:10,Dine:0,Titan:0,Seichi:38,Fray:300,Vertigo:30,Integrity:25,Collateral:40,Hydro:38,USC Vortex:12";
}