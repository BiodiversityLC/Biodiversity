using Biodiversity.Core.Attributes;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

[CreaturePatch("Aloe")]
[HarmonyPatch(typeof(PlayerControllerB))]
internal static class PlayerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("IHittable.Hit")]
    private static bool HitOverride(PlayerControllerB __instance, int force, Vector3 hitDirection,
        PlayerControllerB playerWhoHit, bool playHitSFX = false)
    {
        // Makes sure the player does not take damage if they are being kidnapped
        return !AloeSharedData.Instance.IsPlayerKidnapBound(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("DamagePlayer")]
    private static bool DamagePlayerPatch(PlayerControllerB __instance, int damageNumber, CauseOfDeath causeOfDeath)
    {
        return !AloeSharedData.Instance.IsPlayerKidnapBound(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("KillPlayer")]
    private static bool KillPlayerPatch(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
    {
        return !AloeSharedData.Instance.IsPlayerKidnapBound(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch("AllowPlayerDeath")]
    private static void AllowPlayerDeathPatch(PlayerControllerB __instance, ref bool __result)
    {
        __result = !AloeSharedData.Instance.IsPlayerKidnapBound(__instance);
    }
}