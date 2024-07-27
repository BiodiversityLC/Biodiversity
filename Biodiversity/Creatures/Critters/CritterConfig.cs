using BepInEx.Configuration;
using Biodiversity.Util.Config;
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
}