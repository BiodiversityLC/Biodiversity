using Biodiversity.General;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace Biodiversity.Patches;

// FIXME: This patch is currently broken and like yeah... dont
//[HarmonyPatch(typeof(RoundManager))]
internal static class RoundManagerPatch {
    internal static Dictionary<EnemyType, Func<bool>> spawnRequirements = [];

    static bool CanEnemySpawn(EnemyType type) {
        if(spawnRequirements.TryGetValue(type, out Func<bool> callback)) {
            BiodiversityPlugin.Logger.LogInfo($"doing callback for {type.enemyName}");
            bool result = callback();
            if(!result)
                BiodiversityPlugin.Logger.LogInfo($"Callback for {type.enemyName} blocked the spawning!");

            return result;
        } else {
            return true;
        }
    }

    [HarmonyPatch(nameof(RoundManager.SpawnRandomOutsideEnemy)), HarmonyPatch(nameof(RoundManager.SpawnRandomDaytimeEnemy)), HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ComplexSpawningRequirementsOutside(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        return new CodeMatcher(instructions, generator)
            .MatchForward(true, 
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(RoundManager), nameof(RoundManager.SpawnProbabilities))),
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Callvirt),
                new CodeMatch(OpCodes.Br)
            )
            .Advance(1)
            .ThrowIfInvalid("failed to find spawn probability assignment")
            .CreateLabel(out Label addEnemySpawn)
            .Start()
            .MatchForward(false,
                new CodeMatch(OpCodes.Ldloc_1),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EnemyType), nameof(EnemyType.spawningDisabled))),
                new CodeMatch(OpCodes.Brfalse)
            )
            .ThrowIfInvalid("failed to find if condition")
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RoundManagerPatch), nameof(CanEnemySpawn))),
                new CodeInstruction(OpCodes.Brfalse, addEnemySpawn)
            )
            .InstructionEnumeration();
    }

    [HarmonyPatch(nameof(RoundManager.EnemyCannotBeSpawned)), HarmonyPostfix]
    static void ComplexSpawningRequirementsInside(RoundManager __instance, int enemyIndex, ref bool __result) {
        __result = __result && CanEnemySpawn(__instance.currentLevel.Enemies[enemyIndex].enemyType);
    }
}
