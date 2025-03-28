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
using Biodiversity.Creatures.StateMachine;
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

    internal static CachedList<Assembly> CachedAssemblies;

    private static readonly (string, string)[] SillyQuotes =
    [
        ("don't get me wrong, I love women", "Monty"),
        ("i love MEN with BIG ARMS and STRONGMAN LEGS", "Monty"),
        ("thumpy wumpy", "Monty"),
        ("Your body should get split in two", "Wesley"),
        ("death for you and your bloodline", "Monty"),
        ("the fiend is watching... NOT VERY SIGMA!!", "Rolevote"),
        ("we love fat bitches(gender neutral) and body representation for fat bitches(still gender neutral)", "TiltedHat"),
    ];

    private void Awake()
    {
        Stopwatch timer = Stopwatch.StartNew();
        Logger = BepInEx.Logging.Logger.CreateLogSource(MyPluginInfo.PLUGIN_GUID);
        Instance = this;

        CachedAssemblies = new CachedList<Assembly>(() => AppDomain.CurrentDomain.GetAssemblies().ToList());

        Logger.LogDebug("Creating base biodiversity config."); // Can't use LogVerbose here yet because we need the config to tell us whether verbose logging is enabled or not.
        Config = new BiodiversityConfig(base.Config);

        LogVerbose("Creating Harmony instance...");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        LogVerbose("Running Harmony patches...");
        ApplyPatches();

        LangParser.Init();

        LogVerbose("Patching netcode...");
        NetcodePatcher();

        LogVerbose("Setting up the language translations...");
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
        ).ToList();

        for (int i = 0; i < creatureHandlers.Count; i++)
        {
            Type type = creatureHandlers[i];
            string creatureName = type.Name;
            bool creatureEnabled = base.Config.Bind("Creatures", creatureName, true).Value;
            
            if (!creatureEnabled)
            {
                LogVerbose($"{creatureName} was skipped because it's disabled.");
                continue;
            }
            
            LogVerbose($"Creating {creatureName}");
            type.GetConstructor([])?.Invoke([]);
            
            Config.AddEnabledCreature(creatureName.Replace("Handler", ""));
        }

        LogVerbose($"Sucessfully setup {creatureHandlers.Count} silly creatures!");
        timer.Stop();

        (string, string) quote = SillyQuotes[UnityEngine.Random.Range(0, SillyQuotes.Length)];
        Logger.LogInfo($"\"{quote.Item1}\" - {quote.Item2}");
        LogVerbose(
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
        Dictionary<string, Assembly> modAssemblies = new();
        foreach (Assembly assembly in CachedAssemblies)
        {
            modAssemblies.TryAdd(assembly.GetName().Name, assembly);
        }

        Type[] types = Assembly.GetExecutingAssembly().GetTypes();

        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];

            var creatureAttr = type.GetCustomAttribute<CreaturePatchAttribute>();
            if (creatureAttr != null)
            {
                bool creatureEnabled = Config?.IsCreatureEnabled(creatureAttr.CreatureName) ?? true;
                if (!creatureEnabled)
                {
                    LogVerbose($"Skipping patches for creature '{creatureAttr.CreatureName}' because it is disabled in config.");
                    continue;
                }
            }
            
            List<ModConditionalPatch> modConditionalAttrs =
                type.GetCustomAttributes<ModConditionalPatch>(true).ToList();

            if (modConditionalAttrs.Any())
            {
                for (int j = 0; j < modConditionalAttrs.Count; j++)
                {
                    ModConditionalPatch modConditionalAttr = modConditionalAttrs[j];
                    string assemblyName = modConditionalAttr.AssemblyName;
                    string targetClassName = modConditionalAttr.TargetClassName;
                    string targetMethodName = modConditionalAttr.TargetMethodName;
                    string localPatchMethodName = modConditionalAttr.LocalPatchMethodName;
                    HarmonyPatchType patchType = modConditionalAttr.PatchType;

                    // Check if the required mod is installed; skip if not found
                    if (!modAssemblies.TryGetValue(assemblyName, out Assembly otherModAssembly))
                        continue;

                    LogVerbose(
                        $"Mod {assemblyName} is installed! Patching {targetClassName}.{targetMethodName} with {localPatchMethodName}");

                    // Get the target class; skip if null
                    Type targetClass;

                    try
                    {
                        targetClass = otherModAssembly.GetType(targetClassName);
                    }
                    catch (Exception e) when (
                        e is ArgumentException or ArgumentNullException or FileNotFoundException or FileLoadException
                            or BadImageFormatException)
                    {
                        LogVerbose(
                            $"Could not patch because an exception occurred while getting the target class '{targetClassName}': {e.Message}");
                        continue;
                    }

                    if (targetClass == null)
                    {
                        LogVerbose($"Could not patch because the target class '{targetClassName}' is null.");
                        continue;
                    }

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                               BindingFlags.Instance;

                    // Get the target method; skip if null
                    MethodInfo targetMethod = null;
                    for (int k = 0; k < targetClass.GetMethods(flags).Length; k++)
                    {
                        MethodInfo m = targetClass.GetMethods(flags)[k];
                        if (m.Name != targetMethodName) continue;
                        targetMethod = m;
                        break;
                    }
                    
                    if (targetMethod == null)
                    {
                        LogVerbose($"Could not patch because the target method '{targetMethodName}' is null.");
                        continue;
                    }

                    // Get the local patch method; skip if null
                    MethodInfo localPatchMethod;
                    try
                    {
                        localPatchMethod = type.GetMethod(localPatchMethodName,
                            BindingFlags.NonPublic | BindingFlags.Static);
                    }
                    catch (Exception e) when (e is AmbiguousMatchException or ArgumentException)
                    {
                        LogVerbose(
                            $"Could not patch because an exception occured while getting the local patch method '{localPatchMethodName}': {e.Message}");
                        continue;
                    }

                    if (localPatchMethod == null)
                    {
                        LogVerbose(
                            $"Could not patch because the local patch method '{localPatchMethodName}' is null.");
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
                            Logger.LogError(
                                $"Could not patch because patch type '{patchType.ToString()}' is incompatible.");
                            break;
                    }

                    LogVerbose(
                        $"Successfully patched {targetClassName}.{targetMethodName} with {localPatchMethodName} as {patchType}");
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
        IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetLoadableTypes();
        foreach (Type type in types)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
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

    internal static AssetBundle LoadBundle(string assetBundleName)
    {
        AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
            throw new InvalidOperationException($"Could not find assetbundle: {assetBundleName}"), "AssetBundles",
            assetBundleName));
        
        LogVerbose($"[AssetBundle Loading] {assetBundleName} contains these objects: {string.Join(",", bundle.GetAllAssetNames())}");
        return bundle;
    }

    internal static void LogVerbose(object message)
    {
        if (Config.VerboseLogging)
            Logger.LogDebug(message);
    }
}