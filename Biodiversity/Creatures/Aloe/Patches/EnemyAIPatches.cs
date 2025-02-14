using Biodiversity.Util;
using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

[HarmonyPatch(typeof(EnemyAI))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class EnemyAIPatches
{
    // [HarmonyPatch("PlayerIsTargetable")]
    // [HarmonyPrefix]
    // private static bool PlayerIsTargetablePatch(EnemyAI __instance, PlayerControllerB playerScript, ref bool __result)
    // {
    //     // Stops the player from being targeted by enemies if they are being kidnapped
    //     if (!AloeSharedData.Instance.IsPlayerKidnapBound(playerScript)) return true;
    //     __result = false;
    //     return false;
    // }
    
    [HarmonyPatch(nameof(EnemyAI.PlayerIsTargetable))]
    [HarmonyPostfix]
    private static void OverridePlayerIsTargetable(
        EnemyAI __instance,
        ref bool __result,
        PlayerControllerB playerScript,
        bool cannotBeInShip,
        bool overrideInsideFactoryCheck)
    {
        if (!__result) return; // If the player is already unable to be targeted, then we need no further interventions
        if (playerScript != null && !PlayerUtil.IsPlayerDead(playerScript) && AloeSharedData.Instance.IsPlayerKidnapBound(playerScript))
            __result = false;
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
        if (__result != null && !PlayerUtil.IsPlayerDead(__result) && AloeSharedData.Instance.IsPlayerKidnapBound(__result))
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
        if (__result != null && !PlayerUtil.IsPlayerDead(__result) && AloeSharedData.Instance.IsPlayerKidnapBound(__result))
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
        if (targetPlayer != null && !PlayerUtil.IsPlayerDead(targetPlayer) &&
            (AloeSharedData.Instance.IsPlayerKidnapBound(targetPlayer) || 
             (__instance is FlowermanAI && AloeSharedData.Instance.IsPlayerStalkBound(targetPlayer))))
            __instance.targetPlayer = null;
    }
}