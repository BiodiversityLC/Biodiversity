using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Biodiversity.Creatures.Enemy;
using Biodiversity.Creatures.HoneyFeeder;
using Biodiversity.Creatures.Murkydere;
using Biodiversity.General;
using Biodiversity.Util;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static LethalLib.Modules.Levels;

namespace Biodiversity;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
public class BiodiversityPlugin : BaseUnityPlugin {
    public static BiodiversityPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger { get; private set; }

    bool OgoEnabledValue;

    static ConfigEntry<bool> OgoEnabled;
    static ConfigEntry<bool> VerminEnabled;
    static ConfigEntry<string> levelsOgo;
    static ConfigEntry<string> levelsVermin;
    public static ConfigEntry<float> OgoDetectionRange;
    public static ConfigEntry<float> OgoLoseRange;
    public static ConfigEntry<float> OgoAttackDistance;

    private void Awake() {
        Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
        Instance = this;

        Logger.LogInfo("Running Harmony patches...");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), MyPluginInfo.PLUGIN_GUID);

        Logger.LogInfo("Patching netcode.");
        NetcodePatcher();

        Logger.LogInfo("Getting assets.");
        BiodiverseAssets.Init();

        // TODO: Swap this to LLL once it gets enemy support.
        Logger.LogInfo("Registering the silly little creatures.");
        //Enemies.RegisterEnemy(BiodiverseAssets.HoneyFeeder, Enemies.SpawnType.Outside);

        OgoEnabled = Config.Bind("General", "Enable Ogopogo", true, "Turn to false to disable Ogopogo spawning");
        VerminEnabled = Config.Bind("General", "Enable Vermin", true, "Turn to false to disable Vermin spawning");
        levelsOgo = Config.Bind("General", "Ogopogo Levels", "MarchLevel:100,AdamanceLevel:100", "The moons that Ogopogo will spawn on");
        levelsVermin = Config.Bind("General", "Vermin Levels", "All:100", "The moons that Vermin will spawn on");
        OgoDetectionRange = Config.Bind("General", "Ogopogo detection range", 45f, "The range that Ogopogo will detect you at");
        OgoLoseRange = Config.Bind("General", "Ogopogo lose range", 70f, "The range that Ogopogo will lose you at");
        OgoAttackDistance = Config.Bind("General", "Ogopogo attack distance", 30f, "The distance that Ogopogo will attack you at");

        OgoEnabledValue = false;

        if (OgoDetectionRange.Value >= OgoLoseRange.Value)
        {
            Logger.LogInfo("Ogopogo detection range lower than lose range. Disabling Ogopogo spawning until this is fixed.");
        }
        else if (OgoAttackDistance.Value > OgoDetectionRange.Value)
        {
            Logger.LogInfo("Ogopogo attack distance is not in the detection distance. Disabling Ogopogo spawning until this is fixed.");
        }
        else
        {
            OgoEnabledValue = OgoEnabled.Value;
        }


        (Dictionary<LevelTypes, int> OgoLevelType, Dictionary<string, int> OgoCustomLevelType) = SolveLevels(levelsOgo.Value, OgoEnabledValue);
        (Dictionary<LevelTypes, int> VerminLevelType, Dictionary<string, int> VerminCustomLevelType) = SolveLevels(levelsVermin.Value, VerminEnabled.Value);


        Enemies.RegisterEnemy(BiodiverseAssets.Ogopogo, Enemies.SpawnType.Daytime, OgoLevelType, OgoCustomLevelType, BiodiverseAssets.OgopogoNode, BiodiverseAssets.OgopogoKeyword);
        Enemies.RegisterEnemy(BiodiverseAssets.Vermin, Enemies.SpawnType.Outside, VerminLevelType, VerminCustomLevelType, BiodiverseAssets.VerminNode, BiodiverseAssets.VerminKeyword);

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    //Totally didn't copy this from sirenhead because I didn't want to write it again
    (Dictionary<LevelTypes, int> spawnRateByLevelType, Dictionary<string, int> spawnRateByCustomLevelType) SolveLevels(string config, bool enabled)
    {
        Dictionary<LevelTypes, int> spawnRateByLevelType = new Dictionary<LevelTypes, int>();
        Dictionary<string, int> spawnRateByCustomLevelType = new Dictionary<string, int>();

        string[] configSplit = config.Split(',');

        foreach (string entry in configSplit)
        {
            string[] levelDef = entry.Trim().Split(':');

            if (levelDef.Length != 2)
            {
                continue;
            }

            int spawnrate = 0;

            if (!int.TryParse(levelDef[1], out spawnrate))
            {
                continue;
            }

            if (Enum.TryParse<LevelTypes>(levelDef[0], true, out LevelTypes levelType))
            {
                spawnRateByLevelType[levelType] = spawnrate;
                Logger.LogInfo($"Registered spawn rate for level type {levelType} to {spawnrate}");
            }
            else
            {
                spawnRateByCustomLevelType[levelDef[0]] = spawnrate;
                Logger.LogInfo($"Registered spawn rate for custom level type {levelDef[0]} to {spawnrate}");
            }
        }

        if (enabled)
        {
            return (spawnRateByLevelType, spawnRateByCustomLevelType);
        }
        else
        {
            return (null, null);
        }
    }

    private void NetcodePatcher() {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach(var type in types) {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach(var method in methods) {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if(attributes.Length > 0) {
                    method.Invoke(null, null);
                }
            }
        }
    }
}
