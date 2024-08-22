using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

[HarmonyPatch(typeof(EnemyAI))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class EnemyAIPatches
{
    [HarmonyPatch("PlayerIsTargetable")]
    [HarmonyPrefix]
    private static bool PlayerIsTargetablePatch(EnemyAI __instance, PlayerControllerB playerScript, ref bool __result)
    {
        // Stops the player from being targeted by enemies if they are being kidnapped
        if (!AloeSharedData.Instance.IsPlayerKidnapBound(playerScript)) return true;
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(EnemyAI.CheckLineOfSightForPlayer))]
    [HarmonyPostfix]
    private static void OverrideCheckForPlayersInLineOfSight(
        EnemyAI __instance,
        ref PlayerControllerB __result,
        float width,
        int range,
        int proximityAwareness)
    {
        if (__result != null && AloeSharedData.Instance.IsPlayerKidnapBound(__result))
            __result = null;
    }
    
    [HarmonyPatch(nameof(EnemyAI.CheckLineOfSightForClosestPlayer))]
    [HarmonyPostfix]
    private static void OverrideCheckLineOfSightForClosestPlayer(
        EnemyAI __instance,
        ref PlayerControllerB __result,
        float width,
        int range,
        int proximityAwareness,
        float bufferDistance)
    {
        if (__result != null && AloeSharedData.Instance.IsPlayerKidnapBound(__result))
            __result = null;
    }

    [HarmonyPatch(nameof(EnemyAI.TargetClosestPlayer))]
    [HarmonyPostfix]
    private static void SetTargetPlayerToNull(
        EnemyAI __instance,
        float bufferDistance,
        bool requireLineOfSight,
        float viewWidth)
    {
        PlayerControllerB targetPlayer = __instance.targetPlayer;
        if (targetPlayer != null && (AloeSharedData.Instance.IsPlayerKidnapBound(targetPlayer) ||
                                     (__instance is FlowermanAI &&
                                      AloeSharedData.Instance.IsPlayerStalkBound(targetPlayer))))
            __instance.targetPlayer = null;
    }
}