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
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using BepInEx.Configuration;
using Biodiversity.Creatures;
using Dissonance;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Biodiversity;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
public class BiodiversityPlugin : BaseUnityPlugin {
    public static BiodiversityPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger { get; private set; }

    internal TextWriter LogFile { get; private set; }

    internal new static BiodiversityConfig Config { get; private set; }
    
    static readonly (string,string)[] silly_quotes = [
        ("don't get me wrong, I love women", "monty"), 
        ("i love MEN with BIG ARMS and STRONGMAN LEGS", "monty"),
        ("thumpy wumpy", "monty"),
        ("Your body should get split in two", "wesley"),
        ("death for you and your bloodline", "monty")
    ];

    private void Awake() {
        Stopwatch timer = Stopwatch.StartNew();
        Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
        Instance = this;
        

        Logger.LogInfo("Setting up custom log file.");
        if(Utility.TryOpenFileStream(Path.Combine(Paths.BepInExRootPath, "Biodiversity.log"), FileMode.Create, out FileStream stream)) {
            LogFile = TextWriter.Synchronized(new StreamWriter(stream, Utility.UTF8NoBom));
            Logger.LogEvent += (logger, logEvent) => {
                LogFile.WriteLine(logEvent.ToString());
            };
            new Timer(o => { LogFile.Flush(); }, null, 2000, 2000);
        } else {
            Logger.LogError("Failed to open custom log.");
        }
        
        Logger.LogInfo("Running Harmony patches...");
        if(LogFile != null)
            Logger.LogInfo("(Biodiversity logs will now be routed to Biodiversity.log)");
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), MyPluginInfo.PLUGIN_GUID);
        
        Logger.LogInfo("Creating base biodiversity config.");
        Config = new BiodiversityConfig(base.Config);
        
        Logger.LogInfo("Patching netcode.");
        NetcodePatcher();

        Logger.LogInfo("Doing language stuff");
        LangParser.Init();
        LangParser.SetLanguage("en");

        Logger.LogInfo(LangParser.GetTranslation("lang.test"));
        
        timer.Stop();
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has setup. ({timer.ElapsedMilliseconds}ms)");
    }

    internal void FinishLoading() {
        Stopwatch timer = Stopwatch.StartNew();
        VanillaEnemies.Init();
        
        Logger.LogInfo("Registering the silly little creatures.");
        List<Type> creatureHandlers = Assembly.GetExecutingAssembly().GetLoadableTypes().Where(x =>
            x.BaseType != null
            && x.BaseType.IsGenericType
            && x.BaseType.GetGenericTypeDefinition() == typeof(BiodiverseAIHandler<>)
            && x.Name != "HoneyFeederHandler"
        ).ToList();
        
        foreach(Type type in creatureHandlers) {
            bool creatureEnabled = base.Config.Bind("Creatures", type.Name, true).Value;
            if (!creatureEnabled) {
                Logger.LogWarning($"{type.Name} was skipped because it's disabled.");
                continue;
            }
            Logger.LogDebug($"Creating {type.Name}");
            type.GetConstructor([]).Invoke([]);
        }
        Logger.LogInfo($"Sucessfully setup {creatureHandlers.Count} silly creatures!");

        timer.Stop();
        
        (string, string) quote = silly_quotes[UnityEngine.Random.Range(0, silly_quotes.Length)];
        Logger.LogInfo($"\"{quote.Item1}\" - {quote.Item2}");
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded! ({timer.ElapsedMilliseconds}ms)");
    }

    private void NetcodePatcher() {
        var types = Assembly.GetExecutingAssembly().GetLoadableTypes();
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

    internal ConfigFile CreateConfig(string name) {
        return new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, "me.biodiversity." + name + ".cfg"), saveOnInit: false, MetadataHelper.GetMetadata(this));
    }
}
