using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for Landmine Patches.
/// It makes sure the Aloe and the player don't get blown up if the Aloe goes over a landmine while kidnapping
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Landmine))]
internal class LandminePatch
{
    [HarmonyPatch(nameof(Landmine.OnTriggerEnter))]
    [HarmonyPrefix]
    private static bool PrefixTriggerEntry(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true;

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

    [HarmonyPatch(nameof(Landmine.OnTriggerExit))]
    [HarmonyPrefix]
    private static bool PrefixTriggerExit(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true;
        
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
}