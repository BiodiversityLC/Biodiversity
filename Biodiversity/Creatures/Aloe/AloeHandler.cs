using System;
using Biodiversity.General;
using LethalLib.Modules;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.Aloe;
[UsedImplicitly]
internal class AloeHandler : BiodiverseAIHandler<AloeHandler> {
    internal AloeAssets Assets { get; set; }
    internal AloeConfig Config { get; set; }

    public AloeHandler() {
        Assets = new AloeAssets("aloebracken");
        Config = new AloeConfig(BiodiversityPlugin.Instance.CreateConfig("aloe"));

        Assets.EnemyType.PowerLevel = Config.PowerLevel;
        Assets.EnemyType.MaxCount = Config.MaxAmount;

        RegisterEnemyWithConfig(
            Config.AloeEnabled, 
            Config.Rarity,
            Assets.EnemyType,
            Assets.TerminalNode,
            Assets.TerminalKeyword);

        AddSpawnRequirement(Assets.EnemyType, () => false);
    }
    
    // Xu's config setup
    private static void RegisterEnemyWithConfig(bool enemyEnabled, string configMoonRarity, EnemyType enemy, TerminalNode terminalNode, TerminalKeyword terminalKeyword) 
    {
        if (enemyEnabled) 
        { 
            (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) = ConfigParsing(configMoonRarity);
            Enemies.RegisterEnemy(enemy, spawnRateByLevelType, spawnRateByCustomLevelType, terminalNode, terminalKeyword);
        } 
        else 
        {
            Enemies.RegisterEnemy(enemy, 0, Levels.LevelTypes.All, terminalNode, terminalKeyword);
        }
    }
    
    private static (Dictionary<Levels.LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) ConfigParsing(string configMoonRarity) 
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
                BiodiversityPlugin.Logger.LogDebug($"Registered spawn rate for level type {levelType} to {spawnrate}");
            } 
            else 
            {
                // Try appending "Level" to the name and re-attempt parsing
                string modifiedName = name + "Level";
                if (Enum.TryParse(modifiedName, true, out levelType))
                {
                    spawnRateByLevelType[levelType] = spawnrate;
                    BiodiversityPlugin.Logger.LogDebug($"Registered spawn rate for level type {levelType} to {spawnrate}");
                }
                else
                {
                    spawnRateByCustomLevelType[name] = spawnrate;
                    BiodiversityPlugin.Logger.LogDebug($"Registered spawn rate for custom level type {name} to {spawnrate}");
                }
            }
        }
        
        return (spawnRateByLevelType, spawnRateByCustomLevelType);
    }
}