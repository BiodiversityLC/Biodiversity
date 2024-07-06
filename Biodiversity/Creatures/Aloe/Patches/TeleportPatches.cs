using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class TeleportPatches
{
    // Unbinds the player from the aloe if they are teleported during the kidnapping
    [HarmonyPatch("TeleportPlayer")]
    [HarmonyPostfix]
    private static void PostfixTeleportPlayer(PlayerControllerB __instance, Vector3 pos, bool withRotation = false, float rot = 0f, bool allowInteractTrigger = false, bool enableController = true)
    {
        if (!__instance.IsHost && !__instance.IsServer) return;
        if (__instance == null) return;

        if (!AloeSharedData.Instance.AloeBoundKidnaps.ContainsValue(__instance)) return;
        AloeServer aloeAI = AloeSharedData.Instance.AloeBoundKidnaps.FirstOrDefault(x => x.Value == __instance).Key;
        if (aloeAI == null) return;
        aloeAI.SetTargetPlayerEscapedByTeleportation();
        AloeSharedData.Instance.AloeBoundKidnaps.Remove(aloeAI);
    }
}