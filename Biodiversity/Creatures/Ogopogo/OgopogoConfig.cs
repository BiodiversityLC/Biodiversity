using System.Collections.Generic;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using LethalLib.Modules;
using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo;

public class OgopogoConfig(ConfigFile configFile) : BiodiverseConfigLoader<OgopogoConfig>(configFile) {
	[field: Header("Vermin")]
	[field: Tooltip("Turn to false to disable Vermin spawning")]
	public bool EnableVermin { get; private set; } = true;

	[field: Tooltip("The range that Ogopogo will detect you at")]
	public float DetectionRange { get; private set; } = 45f;

	[field: Tooltip("The range that Ogopogo will lose you at")]
	public float LoseRange { get; private set; } = 70f;

	[field: Tooltip("The distance that Ogopogo will attack you at")]
	public float AttackDistance { get; private set; } = 30f;

	public EnemyRaritiesPerMoon OgopogoRarity { get; private set; } = new(
		0,
		new Dictionary<Levels.LevelTypes, int>() {
			{ Levels.LevelTypes.VowLevel, 34 },
			{ Levels.LevelTypes.MarchLevel, 55 },
			{ Levels.LevelTypes.AdamanceLevel, 85 }
		},
		new Dictionary<string, int>() {
			{ "Submersion", 15 },
			{ "Corrosion", 1 },
			{ "Aquatis", 25 },
			{ "Seichi", 40 },
			{ "Cesspool", 80 },
			{ "Gorgonzola", 5 }
		}
	);
	
	public EnemyRaritiesPerMoon VerminRarity { get; private set; } = new(
		100
	);
}