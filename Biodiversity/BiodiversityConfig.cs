using System;
using BepInEx.Configuration;
using Biodiversity.Core.Config;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Biodiversity;

public class BiodiversityConfig : BiodiverseConfigLoader<BiodiversityConfig>
{
    [field: NonSerialized] public string Language { get; private set; } = "en";

    #region Dev Config Options
    [field: Header("Development")]

    [field: Tooltip("Whether to log more debug information to the console. 99% of people do NOT need to touch this.")]
    public bool VerboseLoggingEnabled { get; private set; } = false;
    #endregion

    #region Other Config Options
    [field: Header("Other")]

    [field: Tooltip("The stab is real.")]
    public bool StabIsReal { get; private set; } = false;
    #endregion

    private readonly HashSet<string> _enabledCreatures = [];

    internal BiodiversityConfig(ConfigFile configFile) : base(configFile)
    {
        Dictionary<string, string> loadedLanguages = BiodiversityPlugin.Instance.Localization.GetAvailableLanguages();

        AcceptableValueList<string> acceptableLanguages = loadedLanguages != null ?
            new AcceptableValueList<string>(loadedLanguages.Keys.ToArray()) :
            new AcceptableValueList<string>("en", "es", "de", "ru", "fr");

        Language = configFile.Bind(
            "General",
            "Language",
            Language,
            new ConfigDescription(
                "What language should Biodiversity use?\n" +
                "Some languages may also need FontPatcher(https://thunderstore.io/c/lethal-company/p/LeKAKiD/FontPatcher/)\n",
                acceptableLanguages)
        ).Value;
    }

    internal void AddEnabledCreature(string creatureName)
    {
        BiodiversityPlugin.LogVerbose("[BiodiversityConfig] Adding enabled creature: " + creatureName);
        _enabledCreatures.Add(creatureName);
    }

    internal bool IsCreatureEnabled(string creatureName)
    {
        return _enabledCreatures.Contains(creatureName);
    }
}