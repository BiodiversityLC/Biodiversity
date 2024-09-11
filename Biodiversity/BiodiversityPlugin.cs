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

    private Harmony harmony;

    private static readonly (string, string)[] silly_quotes = [
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

        Logger.LogInfo("Creating Harmony instance...");
        harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        
        Logger.LogInfo("Running Harmony patches...");
        ApplyPatches();

        LangParser.Init();
        
        Logger.LogInfo("Creating base biodiversity config.");
        Config = new BiodiversityConfig(base.Config);

        Logger.LogInfo("Patching netcode.");
        NetcodePatcher();

        Logger.LogInfo("Doing language stuff");
        LangParser.SetLanguage(Config.Language);

        Logger.LogInfo(LangParser.GetTranslation("lang.test"));

        timer.Stop();
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has setup. ({timer.ElapsedMilliseconds}ms)");
    }

    internal void FinishLoading()
    {
        Stopwatch timer = Stopwatch.StartNew();
        VanillaEnemies.Init();

        // why does unity not let you preload video clips like audio clips.
        Logger.LogInfo("Loading VideoClip bundle.");
        LoadBundle("biodiversity_video_clips");
        
        Logger.LogInfo("Registering the silly little creatures.");
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
                Logger.LogWarning($"{type.Name} was skipped because it's disabled.");
                continue;
            }
            Logger.LogDebug($"Creating {type.Name}");
            type.GetConstructor([])?.Invoke([]);
        }
        
        Logger.LogInfo($"Sucessfully setup {creatureHandlers.Count} silly creatures!");
        timer.Stop();

        (string, string) quote = silly_quotes[UnityEngine.Random.Range(0, silly_quotes.Length)];
        Logger.LogInfo($"\"{quote.Item1}\" - {quote.Item2}");
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID}:{MyPluginInfo.PLUGIN_VERSION} has loaded! ({timer.ElapsedMilliseconds}ms)");
    }

    private void ApplyPatches()
    {
        //todo: actually make it work
        CachedDictionary<string, Assembly> cachedModAssemblies = new(className => (from assembly in AppDomain.CurrentDomain.GetAssemblies() let targetType = assembly.GetType(className) where targetType != null select assembly).FirstOrDefault());
        Type[] types = Assembly.GetExecutingAssembly().GetTypes();

        foreach (Type type in types)
        {
            List<ModConditionalPatch> modConditionalAttrs = type.GetCustomAttributes<ModConditionalPatch>(true).ToList();
            if (modConditionalAttrs.Any())
            {
                foreach (ModConditionalPatch modConditionalAttr in modConditionalAttrs)
                {
                    string targetClassName = modConditionalAttr.TargetClassName;
                    string targetMethodName = modConditionalAttr.TargetMethodName;
                    bool isStaticMethod = modConditionalAttr.IsStaticMethod;
                    string localPatchMethodName = modConditionalAttr.LocalPatchMethodName;
                    HarmonyPatchType patchType = modConditionalAttr.PatchType;
                    
                    // Check if the required mod is installed
                    Assembly otherModAssembly = cachedModAssemblies[targetClassName];
                    if (otherModAssembly == null)
                        continue;
                    
                    Logger.LogDebug($"Mod {targetClassName} is installed! Patching {targetClassName}.{targetMethodName} with {localPatchMethodName}");
                    Type targetClass = otherModAssembly.GetType(targetClassName);
                    
                    if (targetClass == null)
                    {
                        Logger.LogDebug($"Could not patch due to the target class '{targetClassName}' being null.");
                        continue;
                    }

                    BindingFlags flags = isStaticMethod
                        ? BindingFlags.Public | BindingFlags.Static
                        : BindingFlags.Public | BindingFlags.Instance;
                    MethodInfo targetMethod = targetClass.GetMethod(targetMethodName, flags);
                    if (targetMethod == null)
                    {
                        Logger.LogDebug($"Could not patch due to the target method '{targetMethodName}' being null.");
                        continue;
                    }

                    MethodInfo localPatchMethod = type.GetMethod(localPatchMethodName,
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (localPatchMethod == null)
                    {
                        Logger.LogDebug($"Could not patch due to the local patch method '{localPatchMethodName}' being null.");
                        continue;
                    }

                    HarmonyMethod patchMethod = new(localPatchMethod);
                    switch (patchType)
                    {
                        case HarmonyPatchType.Prefix:
                            harmony.Patch(targetMethod, prefix: patchMethod);
                            break;
                        case HarmonyPatchType.Postfix:
                            harmony.Patch(targetMethod, postfix: patchMethod);
                            break;
                        case HarmonyPatchType.Transpiler:
                            harmony.Patch(targetMethod, transpiler: patchMethod);
                            break;
                        case HarmonyPatchType.Finalizer:
                            harmony.Patch(targetMethod, finalizer: patchMethod);
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
                    harmony.CreateClassProcessor(type).Patch();
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

    internal ConfigFile CreateConfig(string name)
    {
        return new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, "me.biodiversity." + name + ".cfg"), saveOnInit: false, MetadataHelper.GetMetadata(this));
    }
    
    internal AssetBundle LoadBundle(string name) {
        AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException($"Could not find assetbundle: {name}"), "assets", name));
        Logger.LogDebug($"[AssetBundle Loading] {name} contains these objects: {string.Join(",", bundle.GetAllAssetNames())}");

        return bundle;
    }
    
    
}
