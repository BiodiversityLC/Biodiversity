﻿using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

[HarmonyPatch(typeof(KnifeItem))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class DevKnifePatch
{
    [HarmonyPatch(nameof(KnifeItem.ItemActivate))]
    [HarmonyPostfix]
    private static void DealDamageToSelf(GrabbableObject __instance)
    {
        if (__instance.playerHeldBy == null) return;
        __instance.playerHeldBy.DamagePlayer(5);
    }
}