﻿using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for Turret Patches.
/// It makes sure the player doesn't get shot by a turret when they are being kidnapped.
/// </summary>
[HarmonyPatch(typeof(Turret))]
internal class TurretPatch
{
    
    [HarmonyPatch(nameof(Turret.CheckForPlayersInLineOfSight))]
    [HarmonyPostfix]
    private static void PostfixCheckForPlayersInLineOfSight(Turret __instance, ref PlayerControllerB __result, float radius, bool angleRangeCheck)
    {
        if (__result != null && AloeSharedData.Instance.AloeBoundKidnaps.ContainsValue(__result))
        {
            __result = null;
        }
    }
}