using BepInEx.Bootstrap;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Biodiversity.Util;

public static class LethalLibUtils
{
    public static void TranslateTerminalNode(TerminalNode node)
    {
        node.displayText = BiodiversityPlugin.Instance.Localization.GetTranslation(node.displayText);
    }

    public static void RegisterEnemyWithConfig(bool enemyEnabled, string configMoonRarity, EnemyType enemy,
        TerminalNode terminalNode, TerminalKeyword terminalKeyword)
    {
        if (enemyEnabled)
        {
            (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType,
                Dictionary<string, int> spawnRateByCustomLevelType) = ConfigParsing(configMoonRarity);

            Enemies.RegisterEnemy(enemy, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode,
                terminalKeyword);
        }
        else
        {
            Enemies.RegisterEnemy(enemy, 0, Levels.LevelTypes.All, terminalNode, terminalKeyword);
        }
    }

    public static void RegisterScrapWithConfig(string configMoonRarity, Item scrap)
    {
        (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) =
            ConfigParsing(configMoonRarity);
        RegisterScrapWithRuntimeIconSupport(scrap, spawnRateByLevelType, spawnRateByCustomLevelType);
    }

    public static void RegisterScrapWithRuntimeIconSupport(Item spawnableItem, Dictionary<Levels.LevelTypes, int> levelRarities, Dictionary<string, int> customLevelRarities)
    {
        bool removeIcon = false;
        foreach (string plugin in Chainloader.PluginInfos.Keys)
        {
            if (plugin == "com.github.lethalcompanymodding.runtimeicons")
            {
                removeIcon = true;
            }
        }

        if (removeIcon)
        {
            spawnableItem.itemIcon = null;
        }

        LethalLib.Modules.Items.RegisterScrap(spawnableItem, levelRarities, customLevelRarities);
    }

    public static void RegisterShopItemWithConfig(bool enabledScrap, Item item, TerminalNode terminalNode, int itemCost,
        string configMoonRarity)
    {
        LethalLib.Modules.Items.RegisterShopItem(item, null!, null!, terminalNode, itemCost);
        if (enabledScrap) RegisterScrapWithConfig(configMoonRarity, item);
    }

    public static
        (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType,
        Dictionary<string, int> spawnRateByCustomLevelType)
        ConfigParsing(string configMoonRarity)
    {
        Dictionary<Levels.LevelTypes, int> spawnRateByLevelType = new();
        Dictionary<string, int> spawnRateByCustomLevelType = new();

        foreach (string entry in configMoonRarity.Split(',').Select(s => s.Trim()))
        {
            string[] entryParts = entry.Split(':');

            if (entryParts.Length != 2) continue;
            string name = entryParts[0];
            if (!int.TryParse(entryParts[1], out int spawnrate)) continue;

            if (Enum.TryParse(name, true, out Levels.LevelTypes levelType))
            {
                spawnRateByLevelType[levelType] = spawnrate;
                BiodiversityPlugin.LogVerbose($"Registered spawn rate for level type {levelType} to {spawnrate}");
            }
            else
            {
                // Try appending "Level" to the name and re-attempt parsing
                string modifiedName = name + "Level";
                if (Enum.TryParse(modifiedName, true, out levelType))
                {
                    spawnRateByLevelType[levelType] = spawnrate;
                    BiodiversityPlugin.LogVerbose($"Registered spawn rate for level type {levelType} to {spawnrate}");
                }
                else
                {
                    spawnRateByCustomLevelType[name] = spawnrate;
                    BiodiversityPlugin.LogVerbose($"Registered spawn rate for custom level type {name} to {spawnrate}");
                }
            }
        }

        return (spawnRateByLevelType, spawnRateByCustomLevelType);
    }
}