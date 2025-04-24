using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using UnityEngine;

namespace Biodiversity.Items.DeveloperItems;

[Serializable]
public class DeveloperScrapConfig(ConfigFile cfg) : BiodiverseConfigLoader<DeveloperScrapConfig>(cfg)
{
    [field: Header("Developer Items Settings")]
    
    public GenericScrapItem RubberDuck { get; private set; } = new("NethersomeDuckItemData", "Nethersomes duck", weight: 1.05f);
}
