using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity;

public class BiodiversityConfig(ConfigFile configFile) : BiodiverseConfigLoader<BiodiversityConfig>(configFile) {
	[field: Header("Development")]
	[field: Tooltip("Whether to log more debug information to the console.")]
	public bool VerboseLogging { get; private set; } = false;
}