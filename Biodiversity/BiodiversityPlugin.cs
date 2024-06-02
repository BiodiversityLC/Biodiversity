using BepInEx;
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
        Enemies.RegisterEnemy(BiodiverseAssets.Ogopogo, Enemies.SpawnType.Daytime, new Dictionary<LevelTypes, int> { { LevelTypes.MarchLevel, 100 }, { LevelTypes.AdamanceLevel, 100} }, null, BiodiverseAssets.OgopogoNode, BiodiverseAssets.OgopogoKeyword);

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded!");
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
