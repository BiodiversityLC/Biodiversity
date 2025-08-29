using BepInEx.Configuration;
using Biodiversity.Core.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Ogopogo;

public class OgopogoConfig(ConfigFile configFile) : BiodiverseConfigLoader<OgopogoConfig>(configFile) 
{
	[field: Header("Vermin")]
	[field: Tooltip("Turn to false to disable Vermin spawning")]
	public bool EnableVermin { get; private set; } = true;

	[field: Tooltip("The range that Ogopogo will detect you at")]
	public float DetectionRange { get; private set; } = 45f;

	[field: Tooltip("The range that Ogopogo will lose you at")]
	public float LoseRange { get; private set; } = 60f;

	[field: Tooltip("The distance that Ogopogo will attack you at")]
	public float AttackDistance { get; private set; } = 30f;
	
	[field: Tooltip("Whether the Ogopogo will spawn in games.")]
	public bool OgopogoEnabled { get; private set; } = true;

	[field: Tooltip("Spawn weight of the Ogopogo on all moons. WARNING: NO OTHER MOONS OTHER THAN THE ONES PRESENT IN THE DEFAULTS WILL WORK FOR OGOPOGO, HE'S CURRENTLY VERY FINNICKY TO WORK WITH SO HE'S NOT COMPATIBLE WITH MOST MOONS.")]
	public string OgopogoRarity { get; private set; } = "Vow:10,March:65,Adamance:35,Artifice:65,Submersion:12,Seichi:40,Gorgonzola:5,Phaedra:22,Aquatis:0,Cesspool:0,Gloom:0,Bozoros:15,Monarch:5,Oldred:69,Atlantica:52,Polarus:33,Acidir:16,Alcatras:57,Cubatres:8,Filitrios:21,Brutality:25,Phuket:5,Valiance:4,Timbrance:28,Natit:10,Sorrow:10,Kanie:40,Terra:75,Consolidation:30";

	[field: Tooltip("Moons where Ogopogo's wander is disabled.")]
	public string OgopogoWanderDisable { get; private set; } = "VowLevel,Submersion,Gorgonzola,Natit,Bozoros,Phaedra";

	[field: Tooltip("Static spawn positions for certain moons. Format: LevelName:(x,y,z)/(x2,y2,z2);LevelName2:(x,y,z)/(x2,y2,z2)")]
	public string OgopogoStaticSpawns { get; private set; } = "VowLevel:(-104.800003, -22.0610008, 110.330002)/(27, -22.0610008, -61.2000008);AdamanceLevel:(58.1199989, -11.04, -1.85000002)/(52.0800018, -11.04, -12.5900002)";

    [field: Tooltip("Ogopogo mineshaft ambient min timer.")]
	public float OgopogoAmbienceMin { get; private set; } = 90;

    [field: Tooltip("Ogopogo mineshaft ambient max timer.")]
    public float OgopogoAmbienceMax { get; private set; } = 600;

    [field: Tooltip("Spawn weight of the Vermin on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config).")]
	public string VerminRarity { get; private set; } = "All:1";

	[field: Tooltip("Moons where Vermin is disabled during flooding.")]
	public string VerminDisableLevels { get; private set; } = "AdamanceLevel,DineLevel,ArtificeLevel,Etern,Pelagia,Cesspool,Hyve,Affliction";
}
