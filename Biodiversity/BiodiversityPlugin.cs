using BepInEx;
using BepInEx.Logging;
using Biodiversity.Creatures.HoneyFeeder;
using Biodiversity.Creatures.Murkydere;
using Biodiversity.General;
using Biodiversity.Util;
using Biodiversity.Util.Lang;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        Logger.LogInfo("Doing language stuff");
        LangParser.Init();
        LangParser.SetLanguage("en");

        Logger.LogInfo(LangParser.GetTranslation("lang.test"));

        // TODO: Swap this to LLL once it gets enemy support.
        Logger.LogInfo("Registering the silly little creatures.");
        List<Type> creatureHandlers = Assembly.GetExecutingAssembly().GetTypes().Where(x =>
            x.BaseType != null
            && x.BaseType.IsGenericType
            && x.BaseType.GetGenericTypeDefinition() == typeof(BiodiverseAIHandler<>)
        ).ToList();

        foreach(var type in creatureHandlers) {
            Logger.LogDebug($"Creating {type.Name}");
            type.GetConstructor([]).Invoke([]);
        }
        Logger.LogInfo($"Sucessfully setup {creatureHandlers.Count} silly creatures!");

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
