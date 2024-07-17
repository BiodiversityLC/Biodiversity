using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

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
}