using BepInEx;
using BepInEx.Logging;
using Biodiversity.Creatures.HoneyFeeder;
using Biodiversity.Creatures.Murkydere;
using Biodiversity.Creatures.WaxSoldier;
using Biodiversity.General;
using Biodiversity.Util;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biodiversity;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
public class BiodiversityPlugin : BaseUnityPlugin
{
    public static BiodiversityPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger { get; private set; }

    internal static HoneyFeederConfig configHoneyFeeder;
    internal static WaxSoldierConfig configWaxSoldier;

    private void Awake()
    {
        Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
        Instance = this;

        Logger.LogInfo("Running Harmony patches...");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), MyPluginInfo.PLUGIN_GUID);

        Logger.LogInfo("Patching netcode.");
        NetcodePatcher();

        Logger.LogInfo("Getting assets.");
        //HoneyFeederAssets assets = new(); //No HoneyFeeder assets in this branch, commenting out stuff related to that
        //Logger.LogInfo("test enemytype: " + assets.enemyType);

        configHoneyFeeder = new HoneyFeederConfig(Config);
        configWaxSoldier = new WaxSoldierConfig(Config);

        // TODO: Swap this to LLL once it gets enemy support.
        Logger.LogInfo("Registering the silly little creatures.");
        //Enemies.RegisterEnemy(assets.enemyType, Enemies.SpawnType.Outside);

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private void NetcodePatcher()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }
}