using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for Landmine patches.
/// It makes sure the Aloe and the player don't get blown up if the Aloe goes over a landmine while kidnapping.
/// </summary>
[CreaturePatch("Aloe")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Landmine))]
internal static class LandminePatch
{
    [HarmonyPatch(nameof(Landmine.OnTriggerEnter))]
    [HarmonyPrefix]
    private static bool PrefixTriggerEntry(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true;
        if (AloeHandler.Instance.Config.LandminesBlowUpAloe) return true;

        AloeServerAI aloeAI = other.gameObject.GetComponentInParent<AloeServerAI>();
        if (aloeAI != null && AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(aloeAI.BioId))
            return false;

        PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
        if (!PlayerUtil.IsPlayerDead(component) && AloeSharedData.Instance.IsPlayerKidnapBound(component))
            return false;

        return true;
    }

    [HarmonyPatch(nameof(Landmine.OnTriggerExit))]
    [HarmonyPrefix]
    private static bool PrefixTriggerExit(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true;
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