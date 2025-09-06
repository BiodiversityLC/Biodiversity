using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Biodiversity.Core.Attributes;
using Biodiversity.Core.Lang;
using Biodiversity.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Biodiversity.Creatures;
using Biodiversity.Items;
using Biodiversity.Util.DataStructures;
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
    internal LangParser Localization { get; private set; }

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
        ("Aloehood is a spectrum", "Ccode"),
        ("Ogopogo is just a giant man buried in the ground grabbing you w his toes", "Monty")
    ];

    private void Awake()
    {
        Stopwatch timer = Stopwatch.StartNew();

        Logger = BepInEx.Logging.Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_NAME}|{MyPluginInfo.PLUGIN_VERSION}");
        Instance = this;
        CachedAssemblies = new CachedList<Assembly>(() => AppDomain.CurrentDomain.GetAssemblies().ToList());

        // Can't use LogVerbose here yet because we need the config to tell us whether verbose logging is enabled or not.

        Logger.LogDebug("Setting up localization...");
        try
        {
            Localization = new LangParser(Assembly.GetExecutingAssembly());
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize LangParser. Error: {ex.Message}");
        }

        Logger.LogDebug("Creating base biodiversity config...");
        Config = new BiodiversityConfig(base.Config);

        // We can now use LogVerbose

        LogVerbose("Creating Harmony instance...");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        LogVerbose("Applying essential Harmony patches...");
        try
        {
            _harmony.CreateClassProcessor(typeof(Patches.GameNetworkManagerPatch)).Patch();
            LogVerbose("Successfully patched GameNetworkManager.");
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to apply GameNetworkManagerPatch: {e}");
        }

        Localization.SetLanguage(Config.Language);
        LogVerbose($"Testing lang.test: {Localization.GetTranslation("lang.test")}");

        NetcodePatcher();

        timer.Stop();
        Logger.LogInfo(
            $"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has setup in {timer.ElapsedMilliseconds}ms.");
    }

    /// <summary>
    /// Finalizes the loading process for the plugin, initializes enemies & items, loads assets, and registers creatures.
    /// Also logs a silly quote and the total time taken for loading.
    /// </summary>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>Initializes the <see cref="VanillaEnemies"/> class.</item>
    /// <item>Loads a specific asset bundle for video clips.</item>
    /// <item>Registers AI handlers for the creatures and logs it.</item>
    /// <item>Registers item handlers for the items and logs it.</item>
    /// <item>Logs a silly quote.</item>
    /// </list>
    /// The method measures the total loading time using a stopwatch and logs the time taken.
    /// </remarks>
    internal void FinishLoading()
    {
        Stopwatch timer = Stopwatch.StartNew();
        LogVerbose("Starting FinishLoading...");

        VanillaEnemies.Init();

        // why does unity not let you preload video clips like audio clips.
        LogVerbose("Loading VideoClip bundle.");
        LoadBundle("biodiversity_video_clips");

        LogVerbose("Registering the creatures...");
        List<Type> creatureHandlers = Assembly.GetExecutingAssembly().GetLoadableTypes().Where(x =>
            x.BaseType is { IsGenericType: true }
            && x.BaseType.GetGenericTypeDefinition() == typeof(BiodiverseAIHandler<>)
        ).ToList();

        int enabledCreatureCount = 0;
        for (int i = 0; i < creatureHandlers.Count; i++)
        {
            Type type = creatureHandlers[i];
            string handlerName = type.Name;

            DisableEnemyByDefaultAttribute dis = type.GetCustomAttribute<DisableEnemyByDefaultAttribute>();
            bool enableByDefault = dis == null;

            bool creatureEnabled = base.Config.Bind("Creatures", handlerName, enableByDefault, $"Enable/disable the {handlerName}").Value;

            if (!creatureEnabled)
            {
                LogVerbose($"{handlerName} was skipped because it's disabled.");
                continue;
            }

            LogVerbose($"Creating {handlerName}...");
            try
            {
                type.GetConstructor([])?.Invoke([]);
                Config.AddEnabledCreature(handlerName.Replace("Handler", ""));
                enabledCreatureCount++;
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to instantiate creature handler {handlerName}: {e}.");
            }
        }

        LogVerbose($"Sucessfully setup {enabledCreatureCount} creatures!");

        LogVerbose("Registering the items...");
        List<Type> itemHandlers = Assembly.GetExecutingAssembly().GetLoadableTypes().Where(x =>
            x.BaseType is { IsGenericType: true }
            && x.BaseType.GetGenericTypeDefinition() == typeof(BiodiverseItemHandler<>)
        ).ToList();

        for (int i = 0; i < itemHandlers.Count; i++)
        {
            Type type = itemHandlers[i];
            string handlerName = type.Name;

            LogVerbose($"Creating {handlerName}...");

            try
            {
                type.GetConstructor([])?.Invoke([]);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to instantiate item handler {handlerName}: {e}.");
            }
        }

        ApplyPatches();

        timer.Stop();

        (string, string) quote = SillyQuotes[UnityEngine.Random.Range(0, SillyQuotes.Length)];
        Logger.LogInfo($"\"{quote.Item1}\" - {quote.Item2}");
        LogVerbose(
            $"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded! ({timer.ElapsedMilliseconds}ms).");
    }

    /// <summary>
    /// Applies Harmony patches dynamically based on attributes found in the current assembly.
    /// </summary>
    /// <remarks>
    /// This method applies patches based on the <see cref="ModConditionalPatch"/> and <see cref="HarmonyPatch"/> attributes.
    /// It works by:
    /// <list type="bullet">
    /// <item>Checking if a required mod (specified by <see cref="ModConditionalPatch"/>) is loaded.</item>
    /// <item>If the mod is loaded, it applies the appropriate patch (prefix, postfix, transpiler, or finalizer).</item>
    /// <item>If no <see cref="ModConditionalPatch"/> is found, it falls back to applying patches using the <see cref="HarmonyPatch"/> attribute.</item>
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

            if (type == typeof(Patches.GameNetworkManagerPatch))
                continue;

            CreaturePatchAttribute creatureAttr = type.GetCustomAttribute<CreaturePatchAttribute>();
            if (creatureAttr != null)
            {
                bool creatureEnabled = Config.IsCreatureEnabled(creatureAttr.CreatureName);
                if (!creatureEnabled)
                {
                    LogVerbose($"Skipping patches in type '{type.FullName}' for creature '{creatureAttr.CreatureName}' because its disabled in config.");
                    continue;
                }

                LogVerbose($"Patches in type '{type.FullName}' for creature '{creatureAttr.CreatureName}' are ENABLED.");
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
                    try
                    {
                        _harmony.CreateClassProcessor(type).Patch();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to apply Harmony patch(es) for type '{type.FullName}': {e}");
                    }
                }
            }
        }
    }

    private static void NetcodePatcher()
    {
        try
        {
            IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetLoadableTypes();
            foreach (Type type in types)
            {
                MethodInfo[] methods =
                    type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];

                    if (!Attribute.IsDefined(method, typeof(RuntimeInitializeOnLoadMethodAttribute)))
                        continue;

                    // Needed because patching the network stuff in the generic StateManagedAI class produces an error
                    if (method.ContainsGenericParameters)
                    {
                        Logger.LogDebug(
                            $"[NetcodePatcher] Skipping generic method {type.FullName}.{method.Name} with [RuntimeInitializeOnLoadMethod] attribute.");
                        continue;
                    }

                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch (Exception invokeException)
                    {
                        Logger.LogError($"Error invoking method {type.FullName}.{method.Name}: {invokeException}");
                    }
                }
            }
        }
        catch (ReflectionTypeLoadException reflectionException)
        {
            Logger.LogError($"[NetcodePatcher] Error loading types from assembly: {reflectionException}");

            for (int i = 0; i < reflectionException.LoaderExceptions.Length; i++)
            {
                Exception loaderException = reflectionException.LoaderExceptions[i];
                if (loaderException != null)
                {
                    Logger.LogError($"[NetcodePatcher] Loader Exception: {loaderException.Message}");
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
        AssetBundle bundle;
        try
        {
            bundle = AssetBundle.LoadFromFile(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                throw new InvalidOperationException($"Could not find assetbundle: {assetBundleName}"), "AssetBundles",
                assetBundleName));
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Could not load assetbundle: {e}");
            return null;
        }

        LogVerbose($"[AssetBundle Loading] {assetBundleName} contains these objects: {string.Join(",", bundle.GetAllAssetNames())}");
        return bundle;
    }

    internal static void LogVerbose(object message)
    {
        if (Config.VerboseLoggingEnabled)
            Logger.LogDebug(message);
    }
}