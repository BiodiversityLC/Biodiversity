using BepInEx;
using BepInEx.Logging;
using Biodiversity.Creatures.Murkydere;
using Biodiversity.General;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biodiversity;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
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
