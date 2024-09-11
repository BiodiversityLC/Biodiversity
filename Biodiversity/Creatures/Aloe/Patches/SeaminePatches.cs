using Biodiversity.Util.Attributes;
using GameNetcodeStuff;
using HarmonyLib;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for Seamine patches.
/// Seamines are from the mod "Surfaced".
/// It makes sure the Aloe and the player don't get blown up if the Aloe goes over a seamine while kidnapping.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[ModConditionalPatch("Surfaced", "Seamine", "OnTriggerEnter", "PrefixTriggerEntry", HarmonyPatchType.Prefix)]
internal class SeaminePatches
{
    [HarmonyPrefix]
    private static bool PrefixTriggerEntry(object __instance, Collider other)
    {
        if (!IsHost(__instance) && !IsServer(__instance)) return true;
        if (AloeHandler.Instance.Config.LandminesBlowUpAloe) return true;

        AloeServer aloeAI = other.gameObject.GetComponentInParent<AloeServer>();
        if (aloeAI != null && AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(aloeAI.aloeId))
        {
            return false;
        }

        PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
        if (component != null && AloeSharedData.Instance.IsPlayerKidnapBound(component) && !component.isPlayerDead)
        {
            return false;
        }

        return true;
    }
    
    // Reflection-based method to check if `__instance.IsHost` is true
    private static bool IsHost(object instance)
    {
        return (bool)instance.GetType().GetProperty("IsHost")?.GetValue(instance)!;
    }

    // Reflection-based method to check if `__instance.IsServer` is true
    private static bool IsServer(object instance)
    {
        return (bool)instance.GetType().GetProperty("IsServer")?.GetValue(instance)!;
    }
}