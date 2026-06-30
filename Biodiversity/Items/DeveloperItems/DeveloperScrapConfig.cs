using BepInEx.Configuration;
using Biodiversity.Core.Config;
using System;
using UnityEngine;

namespace Biodiversity.Items.DeveloperItems;

[Serializable]
public class DeveloperScrapConfig(ConfigFile cfg) : BiodiverseConfigLoader<DeveloperScrapConfig>(cfg)
{
    [field: Header("Developer Items Settings")]

    public GenericScrapItem NethersomeDuck { get; private set; } =
        new(
            "NethersomeDuckItemData", "Nethersome duck",
            rarity: "All:2",
            weight: 0f,
            minimumValue: 1,
            maximumValue: 100);

    /*
    public GenericScrapItem Megaphone { get; private set; } =
        new(
            "MontyMegaphoneItemData", "Megaphone",
            weight: 8f,
            rarity: "Experimentation:2,Embrion:22",
            minimumValue: 28,
            maximumValue: 68);
    */

    public GenericScrapItem IronDog { get; private set; } =
        new GenericScrapItem(
            "AutumnisIronDogItemData", "Iron dog",
            rarity: "Sierra:17,Offense:7,Assurance:10",
            weight: 75f,
            minimumValue: 35,
            maximumValue: 57)
        .WithCustomSetting("Big Shake Distance", 3f,
            "The maximum distance from the Iron Dog at which you will experience a large screen shake when the Iron Dog is dropped on the floor. Set it to -1 to disable it.",
            new AcceptableValueRange<float>(-1f, 1000f))
        .WithCustomSetting("Small Shake Distance", 6f,
            "The maximum distance from the Iron Dog at which you will experience a small screen shake when the Iron Dog is dropped on the floor. Set it to -1 to disable it.",
            new AcceptableValueRange<float>(-1f, 1000f));

    public GenericScrapItem AudunPlush { get; private set; } =
        new(
            "CcodeDogItemData", "Audun plush",
            rarity: "March:14,Rend:4,Gorgonzola:14",
            weight: 4f,
            minimumValue: 20,
            maximumValue: 53);

    public GenericScrapItem StrangeDog { get; private set; } =
        new GenericScrapItem(
            "JacuPlushieItemData", "Strange dog",
            rarity: "Adamance:2,Embrion:5,Trite:2",
            weight: 4f,
            minimumValue: 24,
            maximumValue: 43)
        .WithCustomSetting("Fly effect chance", 5,
            "The chance in % at which the Strange dog will do a strange fly effect instead of a simple noise when used.",
            new AcceptableValueRange<int>(0, 100))
        .WithCustomSetting("Rare squeeze sfx chance", 10,
            "The chance in % at which the Strange dog will do random special rare noise when used instead of the normal squeeze sound.",
            new AcceptableValueRange<int>(0, 100));
}
