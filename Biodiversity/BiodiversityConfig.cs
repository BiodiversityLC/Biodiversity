using System;
using System.Linq;
using BepInEx.Configuration;
using Biodiversity.Util.Config;
using Biodiversity.Util.Lang;
using UnityEngine;

namespace Biodiversity;

public class BiodiversityConfig : BiodiverseConfigLoader<BiodiversityConfig>
{
    [field: Header("Development")]
    [field: Tooltip("Whether to log more debug information to the console. 99% of people do NOT need to touch this.")]
    public bool VerboseLogging { get; private set; } = false;

    [field: NonSerialized] public string Language { get; private set; } = "en";

    [field: Tooltip("The stab is real.")]
    public bool StabIsReal { get; private set; } = false;

    internal BiodiversityConfig(ConfigFile configFile) : base(configFile)
    {
        AcceptableValueList<string> acceptableLanguages = LangParser.Languages.IsNotNull
            ? new AcceptableValueList<string>(LangParser.Languages.Value.Keys.ToArray())
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
}
