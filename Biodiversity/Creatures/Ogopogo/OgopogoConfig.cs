using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo;

public class OgopogoConfig(ConfigFile configFile) : BiodiverseConfigLoader<OgopogoConfig>(configFile) {
	[field: Header("Vermin")]
	[field: Tooltip("Turn to false to disable Vermin spawning")]
	public bool EnableVermin { get; private set; } = true;

	[field: Tooltip("The range that Ogopogo will detect you at")]
	public float DetectionRange { get; private set; } = 45f;

	[field: Tooltip("The range that Ogopogo will lose you at")]
	public float LoseRange { get; private set; } = 60f;

	[field: Tooltip("The distance that Ogopogo will attack you at")]
	public float AttackDistance { get; private set; } = 30f;

	// public EnemyRaritiesPerMoon OgopogoRarity { get; private set; } = new(
	// 	0,
	// 	new Dictionary<Levels.LevelTypes, int>() {
	// 		{ Levels.LevelTypes.VowLevel, 34 },
	// 		{ Levels.LevelTypes.MarchLevel, 55 },
	// 		{ Levels.LevelTypes.AdamanceLevel, 85 }
	// 	},
	// 	new Dictionary<string, int>() {
	// 		{ "Submersion", 15 },
	// 		{ "Corrosion", 1 },
	// 		{ "Aquatis", 25 },
	// 		{ "Seichi", 40 },
	// 		{ "Cesspool", 80 },
	// 		{ "Gorgonzola", 5 }
	// 	}
	// );
	
	[field: Tooltip("Whether the Ogopogo will spawn in games.")]
	public bool OgopogoEnabled { get; private set; } = true;

	[field: Tooltip("Spawn weight of the Ogopogo on all moons. WARNING: NO OTHER MOONS OTHER THAN THE ONES PRESENT IN THE DEFAULTS WILL WORK FOR OGOPOGO, HE'S CURRENTLY VERY FINNICKY TO WORK WITH SO HE'S NOT COMPATIBLE WITH MOST MOONS.")]
	public string OgopogoRarity { get; private set; } = "Vow:10,March:55,Adamance:35,Submersion:15,Seichi:40,Gorgonzola:5,Polarus:0,Aquatis:0,Cesspool:0,Gloom:0";

	[field: Tooltip("Moons where Ogopogo's wander is disabled.")]
	public string OgopogoWanderDisable { get; private set; } = "VowLevel";

	[field: Tooltip("Spawn weight of the Vermin on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string VerminRarity { get; private set; } = "All:100";

	[field: Tooltip("Moons where Vermin is disabled during flooding.")]
	public string VerminDisableLevels { get; private set; } = "AdamanceLevel,DineLevel,ArtificeLevel,Etern";
}
