using System;
using System.Linq;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using Biodiversity.Util.Lang;
using UnityEngine;

namespace Biodiversity;

public class BiodiversityConfig : BiodiverseConfigLoader<BiodiversityConfig> {
    
	[field: Header("Development")]
	[field: Tooltip("Whether to log more debug information to the console.")]
	public bool VerboseLogging { get; private set; } = false;

	[field: NonSerialized]
	public string Language { get; private set; } = "en";
    
	internal BiodiversityConfig(ConfigFile configFile) : base(configFile) {
		Language = configFile.Bind(
			"General",
			"Language",
			Language,
			new ConfigDescription(
				"What language should Biodiversity use?\n" +
				"Some Languages may also need FontPatcher(https://thunderstore.io/c/lethal-company/p/LeKAKiD/FontPatcher/)\\n",
				new AcceptableValueList<string>(LangParser.languages.Keys.ToArray())
			)
		).Value;
	}
}