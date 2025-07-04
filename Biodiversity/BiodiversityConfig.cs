using System;
using BepInEx.Configuration;
using Biodiversity.Core.Config;
using Biodiversity.Core.Lang;
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
        AcceptableValueList<string> acceptableLanguages = LangParser.Languages != null
            ? new AcceptableValueList<string>(LangParser.Languages.Keys.ToArray())
            : new AcceptableValueList<string>("en", "es", "de", "ru");

        Language = configFile.Bind(
            "General",
            "Language",
            Language,
            new ConfigDescription(
                "What language should Biodiversity use (en, es, de, ru)?\n" +
                "Some languages may also need FontPatcher(https://thunderstore.io/c/lethal-company/p/LeKAKiD/FontPatcher/)\\n",
                acceptableLanguages)
        ).Value;
    }

    internal void AddEnabledCreature(string creatureName)
    {
        BiodiversityPlugin.LogVerbose("Adding enabled creature: " + creatureName);
        _enabledCreatures.Add(creatureName);
    }

    internal bool IsCreatureEnabled(string creatureName)
    {
        return _enabledCreatures.Contains(creatureName);
    }
}
