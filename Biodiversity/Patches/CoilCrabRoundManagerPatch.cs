using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.CoilCrab;
using HarmonyLib;
using LethalLib.Modules;
using System;

namespace Biodiversity.Patches;

[CreaturePatch("CoilCrab")]
[HarmonyPatch(typeof(RoundManager))]
internal static class CoilCrabRoundManagerPatch
{
    // Dynamic spawn weights for coil crab
    [HarmonyPatch(nameof(RoundManager.PredictAllOutsideEnemies)), HarmonyPrefix]
    internal static void CoilCrabSpawnWeights(RoundManager __instance)
    {
        BiodiversityPlugin.LogVerbose("Setting Coil crab dynamic weights");

        // assume that the level is vanilla then switch to modded naming if needed
        string levelName = __instance.currentLevel.name;

        // This mess runs on some vanilla level names when they are in the config without "Level" at the end of the weight but it doesn't change anything about them except adding the "Level" back to the end which doesn't effect anything so whatever. It's actually needed for the next line to work also. 
        if (!Enum.IsDefined(typeof(Levels.LevelTypes), levelName))
        {
            levelName = Levels.Compatibility.GetLLLNameOfLevel(levelName);
        }

        levelName = levelName.Remove(levelName.Length - "Level".Length);
        // I don't care if I can write a number instead of using the .Length property. I would just rather have it easy to read.

        BiodiversityPlugin.LogVerbose($"The name of the level (For the Coil-Crab debug): {levelName} ({levelName.Length})");


        SpawnableEnemyWithRarity crab = null;
        foreach (SpawnableEnemyWithRarity enemy in __instance.currentLevel.DaytimeEnemies)
        {
            if (enemy.enemyType.enemyName == "Coil Crab")
            {
                crab = enemy;
            }
        }

        if (crab == null)
        {
            BiodiversityPlugin.LogVerbose("Coil crab was not added in the non-stormy config so the crab will not be enabled on this moon.");
        }

        if (crab != null)
        {
            if (TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Stormy)
            {
                if (CoilCrabHandler.Instance.StormyWeights.ContainsKey(levelName))
                {
                    crab.rarity = CoilCrabHandler.Instance.StormyWeights[levelName];
                }
                else
                {
                    crab.rarity = 0;
                    BiodiversityPlugin.LogVerbose("Coil Crab dynamic weights were set to zero because the stormy weights did not include the current moon.");
                }
            }
            else
            {
                if (CoilCrabHandler.Instance.Weights.ContainsKey(levelName))
                {
                    crab.rarity = CoilCrabHandler.Instance.Weights[levelName];
                }
                else
                {
                    crab.rarity = 0;
                    BiodiversityPlugin.LogVerbose("Coil Crab dynamic weights were set to zero because the non-stormy weights did not include the current moon.");
                }
            }
        }
    }
}