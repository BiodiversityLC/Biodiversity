using Biodiversity.Core.Attributes;
using Biodiversity.Util;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.Rock;

[UsedImplicitly]
// [HideHandler]
internal class RockHandler : BiodiverseAIHandler<RockHandler>
{
    internal RockAssets Assets { get; private set; }
    internal RockConfig Config { get; private set; }

    public Dictionary<string, int> chosenMats = new();

    public RockHandler()
    {
        Assets = new RockAssets("biodiversity_rock");
        Config = new RockConfig(BiodiversityPlugin.Instance.CreateConfig("rock"));


        string[] levels = Config.RockMatsConfig.Split(',');
        foreach (string levelcon in levels)
        {
            string[] parts = levelcon.Split(':');
            if (parts.Length != 2)
            {
                BiodiversityPlugin.Logger.LogWarning($"Invalid Rock material config entry: {levelcon}");
                continue;
            }

            string levelName = parts[0];

            if (int.TryParse(parts[1], out int matIndex))
            {
                BiodiversityPlugin.Logger.LogInfo($"Configured Rock material for {levelName} to {matIndex}");
                chosenMats.Add(levelName, matIndex);
            }
            else
            {
                BiodiversityPlugin.Logger.LogWarning($"Invalid Rock material config entry: {levelName}: {parts[1]}");
            }
        }

        LethalLibUtils.RegisterEnemyWithConfig(Config.RockEnabled, Config.RockRarity, Assets.RockEnemyType, Assets.RockTerminalNode, Assets.RockKeyword);
    }
}