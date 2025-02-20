using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using GameNetcodeStuff;
using HarmonyLib;
using JetBrains.Annotations;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for Seamine patches.
/// Seamines are from the mod "Surfaced".
/// It makes sure the Aloe and the player don't get blown up if the Aloe goes over a seamine while kidnapping.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[UsedImplicitly]
[ModConditionalPatch("Surfaced", "Seamine", "OnTriggerEnter", "PrefixTriggerEntry", HarmonyPatchType.Prefix)]
internal static class SeaminePatches
{
    [UsedImplicitly]
    [HarmonyPrefix]
    private static bool PrefixTriggerEntry(object __instance, Collider other)
    {
        if (!ExtensionMethods.IsHostReflection(__instance) && !ExtensionMethods.IsServerReflection(__instance)) return true;
        if (AloeHandler.Instance.Config.LandminesBlowUpAloe) return true;

        AloeServerAI aloeAI = other.gameObject.GetComponentInParent<AloeServerAI>();
        if (aloeAI != null && AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(aloeAI.BioId))
            return false;

        PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
        if (!PlayerUtil.IsPlayerDead(component) && AloeSharedData.Instance.IsPlayerKidnapBound(component))
            return false;

        return true;
    }
}