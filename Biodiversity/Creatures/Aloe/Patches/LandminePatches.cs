using Biodiversity.Core.Attributes;
using Biodiversity.Util;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// This class is for <see cref="Landmine"/> patches.
/// It makes sure the Aloe (<see cref="AloeServerAI"/>) and <see cref="PlayerControllerB"/> don't get blown up if the Aloe goes over a <see cref="Landmine"/> while kidnapping.
/// </summary>
[CreaturePatch("Aloe")]
[HarmonyPatch(typeof(Landmine))]
[HarmonyPriority(Priority.HigherThanNormal)]
internal static class LandminePatch
{
    [HarmonyPatch(nameof(Landmine.OnTriggerEnter))]
    [HarmonyPrefix]
    private static bool PrefixTriggerEntry(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true; // The Aloe is only owned by the host/server, so theres no point making clients run this code
        if (AloeHandler.Instance.Config.LandminesBlowUpAloe) return true;

        return CheckForLandmineInterventionTargets(other);
    }

    [HarmonyPatch(nameof(Landmine.OnTriggerExit))]
    [HarmonyPrefix]
    private static bool PrefixTriggerExit(Landmine __instance, Collider other)
    {
        if (!__instance.IsHost && !__instance.IsServer) return true; // The Aloe is only owned by the host/server, so theres no point making clients run this code
        if (AloeHandler.Instance.Config.LandminesBlowUpAloe) return true;

        return CheckForLandmineInterventionTargets(other);
    }

    private static bool CheckForLandmineInterventionTargets(Collider other)
    {
        AloeServerAI aloeAI = other.gameObject.GetComponentInParent<AloeServerAI>();
        if (aloeAI && AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(aloeAI.BioId))
            return false;

        PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
        if (!PlayerUtil.IsPlayerDead(player) && AloeSharedData.Instance.IsPlayerKidnapBound(player))
            return false;

        FakePlayerBodyRagdoll fakePlayerBodyRagdoll = other.gameObject.GetComponent<FakePlayerBodyRagdoll>();
        if (fakePlayerBodyRagdoll)
            return false;

        return true;
    }
}