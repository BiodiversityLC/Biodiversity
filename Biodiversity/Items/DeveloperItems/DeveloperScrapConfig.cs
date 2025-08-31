using BepInEx.Configuration;
using Biodiversity.Core.Config;
using System;
using UnityEngine;

namespace Biodiversity.Items.DeveloperItems;

[Serializable]
public class DeveloperScrapConfig(ConfigFile cfg) : BiodiverseConfigLoader<DeveloperScrapConfig>(cfg)
{
    [field: Header("Developer Items Settings")]
    
    public GenericScrapItem RubberDuck { get; private set; } = 
        new(
            "NethersomeDuckItemData", "Nethersomes duck", 
            weight: 1f,
            minimumValue: 5,
            maximumValue: 100);

    /*
    public GenericScrapItem Megaphone { get; private set; } =
        new(
            "MontyMegaphoneItemData", "Montys megaphone",
            weight: 1.08f,
            rarity: "Experimentation:2,Embrion:22",
            minimumValue: 28,
            maximumValue: 68);
    */
    
    public GenericScrapItem IronDog { get; private set; } =
        new(
            "AutumnisIronDogItemData", "Autumnis Iron Dog",
            weight: 1.35f,
            rarity: "Sierra:17,Offense:7,Assurance:10",
            minimumValue: 35,
            maximumValue: 57);
    
    // Temp names, weights, values and rarities for these two below
    public GenericScrapItem CDog { get; private set; } =
        new(
            "CDogItemData", "Ccodes dog",
            weight: 1.05f,
            rarity: "All:20",
            minimumValue: 50,
            maximumValue: 100);
    
    public GenericScrapItem JacuPlushie { get; private set; } =
        new(
            "JacuPlushieItemData", "Dog",
            weight: 1.1f,
            rarity: "All:20",
            minimumValue: 50,
            maximumValue: 100);
}
