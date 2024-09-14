using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Biodiversity.Util;
using Biodiversity.Util.Lang;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Biodiversity.Creatures;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.Types;
using UnityEngine;
using HarmonyPatchType = HarmonyLib.HarmonyPatchType;

namespace Biodiversity;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(LethalLib.Plugin.ModGUID)]
public class BiodiversityPlugin : BaseUnityPlugin
{
    public static BiodiversityPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger { get; private set; }

    internal new static BiodiversityConfig Config { get; private set; }

    private Harmony _harmony;

    private static readonly (string, string)[] SillyQuotes =
    [
        ("don't get me wrong, I love women", "monty"),
        ("i love MEN with BIG ARMS and STRONGMAN LEGS", "monty"),
        ("thumpy wumpy", "monty"),
        ("Your body should get split in two", "wesley"),
        ("death for you and your bloodline", "monty"),
        ("the fiend is watching... NOT VERY SIGMA!!", "rolevote"),
    ];

    private void Awake()
    {
        Stopwatch timer = Stopwatch.StartNew();
        Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
        Instance = this;

        Logger.LogDebug("Creating base biodiversity config.");
        Config = new BiodiversityConfig(base.Config);

        LogVerbose("Creating Harmony instance...");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        LogVerbose("Running Harmony patches...");
        ApplyPatches();

        LangParser.Init();

        LogVerbose("Patching netcode.");
        NetcodePatcher();

        LogVerbose("Doing language stuff");
        LangParser.SetLanguage(Config.Language);

        LogVerbose(LangParser.GetTranslation("lang.test"));

        timer.Stop();
        Logger.LogInfo(
            $"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has setup. ({timer.ElapsedMilliseconds}ms)");
    }

    /// <summary>
    /// Finalizes the loading process for the plugin, initializes enemies, loads assets, and registers creatures.
    /// Also logs a silly quote and the total time taken for loading.
    /// </summary>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>Initializes the <c>VanillaEnemies</c> class.</item>
    /// <item>Loads a specific asset bundle for video clips.</item>
    /// <item>Registers AI handlers for creatures in the plugin, excluding the <c>HoneyFeederHandler</c>.</item>
    /// <item>Logs the registration of each creature handler.</item>
    /// <item>Logs a silly quote.</item>
    /// </list>
    /// The method measures the total loading time using a stopwatch and logs the time taken.
    /// </remarks>
    internal void FinishLoading()
    {
        Stopwatch timer = Stopwatch.StartNew();
        VanillaEnemies.Init();

        // why does unity not let you preload video clips like audio clips.
        LogVerbose("Loading VideoClip bundle.");
        LoadBundle("biodiversity_video_clips");

        LogVerbose("Registering the silly little creatures.");
        List<Type> creatureHandlers = Assembly.GetExecutingAssembly().GetLoadableTypes().Where(x =>
            x.BaseType is { IsGenericType: true }
            && x.BaseType.GetGenericTypeDefinition() == typeof(BiodiverseAIHandler<>)
            && x.Name != "HoneyFeederHandler"
        ).ToList();

        foreach (Type type in creatureHandlers)
        {
            bool creatureEnabled = base.Config.Bind("Creatures", type.Name, true).Value;
            if (!creatureEnabled)
            {
                LogVerbose($"{type.Name} was skipped because it's disabled.");
                continue;
            }

            LogVerbose($"Creating {type.Name}");
            type.GetConstructor([])?.Invoke([]);
        }

        LogVerbose($"Sucessfully setup {creatureHandlers.Count} silly creatures!");
        timer.Stop();

        (string, string) quote = SillyQuotes[UnityEngine.Random.Range(0, SillyQuotes.Length)];
        Logger.LogInfo($"\"{quote.Item1}\" - {quote.Item2}");
        Logger.LogDebug(
            $"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded! ({timer.ElapsedMilliseconds}ms)");
    }

    /// <summary>
    /// Applies Harmony patches dynamically based on attributes found in the current assembly.
    /// </summary>
    /// <remarks>
    /// This method applies patches based on the <see cref="ModConditionalPatch"/> and <see cref="HarmonyPatch"/> attributes. 
    /// It works by:
    /// <list type="bullet">
    /// <item>Checking if a required mod (specified by <c>ModConditionalPatch</c>) is loaded.</item>
    /// <item>If the mod is loaded, it applies the appropriate patch (prefix, postfix, transpiler, or finalizer).</item>
    /// <item>If no <c>ModConditionalPatch</c> is found, it falls back to applying patches using the <c>HarmonyPatch</c> attribute.</item>
    /// </list>
    /// This method handles patch failures by logging reasons such as missing classes, methods, or patch methods.
    /// </remarks>
    private void ApplyPatches()
    {
        CachedDictionary<string, Assembly> cachedModAssemblies = new(assemblyName =>
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assemblyName == assembly.GetName().Name);
        });

        Type[] types = Assembly.GetExecutingAssembly().GetTypes();

        foreach (Type type in types)
        {
            List<ModConditionalPatch> modConditionalAttrs =
                type.GetCustomAttributes<ModConditionalPatch>(true).ToList();
            if (modConditionalAttrs.Any())
            {
                foreach (ModConditionalPatch modConditionalAttr in modConditionalAttrs)
                {
                    string assemblyName = modConditionalAttr.AssemblyName;
                    string targetClassName = modConditionalAttr.TargetClassName;
                    string targetMethodName = modConditionalAttr.TargetMethodName;
                    string localPatchMethodName = modConditionalAttr.LocalPatchMethodName;
                    HarmonyPatchType patchType = modConditionalAttr.PatchType;

                    // Check if the required mod is installed
                    Assembly otherModAssembly = cachedModAssemblies[assemblyName];
                    if (otherModAssembly == null)
                        continue;

                    Logger.LogDebug($"Mod {assemblyName} is installed! Patching {targetClassName}.{targetMethodName} with {localPatchMethodName}");

                    Type targetClass = otherModAssembly.GetType(targetClassName);
                    if (targetClass == null)
                    {
                        Logger.LogDebug($"Could not patch due to the target class '{targetClassName}' being null.");
                        continue;
                    }

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    MethodInfo targetMethod = targetClass.GetMethods(flags).FirstOrDefault(m => m.Name == targetMethodName);
                    if (targetMethod == null)
                    {
                        Logger.LogDebug($"Could not patch due to the target method '{targetMethodName}' being null.");
                        continue;
                    }

                    MethodInfo localPatchMethod = type.GetMethod(localPatchMethodName, BindingFlags.NonPublic | BindingFlags.Static);
                    if (localPatchMethod == null)
                    {
                        Logger.LogDebug($"Could not patch due to the local patch method '{localPatchMethodName}' being null.");
                        continue;
                    }

                    HarmonyMethod patchMethod = new(localPatchMethod);
                    switch (patchType)
                    {
                        case HarmonyPatchType.Prefix:
                            _harmony.Patch(targetMethod, prefix: patchMethod);
                            break;
                        case HarmonyPatchType.Postfix:
                            _harmony.Patch(targetMethod, postfix: patchMethod);
                            break;
                        case HarmonyPatchType.Transpiler:
                            _harmony.Patch(targetMethod, transpiler: patchMethod);
                            break;
                        case HarmonyPatchType.Finalizer:
                            _harmony.Patch(targetMethod, finalizer: patchMethod);
                            break;
                        default:
                            Logger.LogError($"Could not patch because patch type '{patchType.ToString()}' is incompatible.");
                            break;
                    }

                    Logger.LogDebug($"Successfully patched {targetClassName}.{targetMethodName} with {localPatchMethodName} as {patchType}");
                }
            }
            else
            {
                object[] harmonyPatchAttrs = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                if (harmonyPatchAttrs.Length > 0)
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
            }
        }
    }

    private static void NetcodePatcher()
    {
        var types = Assembly.GetExecutingAssembly().GetLoadableTypes();
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

    internal ConfigFile CreateConfig(string configName)
    {
        return new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, "me.biodiversity." + configName + ".cfg"),
            saveOnInit: false, MetadataHelper.GetMetadata(this));
    }
    
    internal AssetBundle LoadBundle(string assetBundleName) 
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException($"Could not find assetbundle: {assetBundleName}"), "assets", assetBundleName));
        LogVerbose($"[AssetBundle Loading] {assetBundleName} contains these objects: {string.Join(",", bundle.GetAllAssetNames())}");

        return bundle;
    }
    
    internal static void LogVerbose(object message)
    {
        if (Config.VerboseLogging) Logger.LogDebug(message);
    }

}