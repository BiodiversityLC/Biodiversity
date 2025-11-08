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
            rarity: "All:2",
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
        new GenericScrapItem(
                "AutumnisIronDogItemData", "Autumnis Iron Dog",
                rarity: "Sierra:17,Offense:7,Assurance:10",
                weight: 1.35f,
                minimumValue: 35,
                maximumValue: 57)
            .WithCustomSetting("Big Shake Distance", 3f,
                "The maximum distance from the Iron Dog at which you will experience a large screen shake when the Iron Dog is dropped on the floor. Set it to -1 to disable it.",
                new AcceptableValueRange<float>(-1f, 1000f))
            .WithCustomSetting("Small Shake Distance", 6f,
                "The maximum distance from the Iron Dog at which you will experience a small screen shake when the Iron Dog is dropped on the floor. Set it to -1 to disable it.",
                new AcceptableValueRange<float>(-1f, 1000f));

    // Temp names, weights, values and rarities for these two below
    public GenericScrapItem CDog { get; private set; } =
        new(
            "CDogItemData", "Ccodes dog",
            rarity: "All:20",
            weight: 1.05f,
            minimumValue: 50,
            maximumValue: 100);

    public GenericScrapItem JacuPlushie { get; private set; } =
        new(
            "JacuPlushieItemData", "Dog",
            rarity: "All:20",
            weight: 1.1f,
            minimumValue: 50,
            maximumValue: 100);
}