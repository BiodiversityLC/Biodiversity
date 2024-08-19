using Biodiversity.Creatures.Aloe.Types.Networking;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
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

        if (!AloeSharedData.Instance.IsPlayerKidnapBound(__instance)) return;
        string aloeId = AloeSharedData.Instance.AloeBoundKidnaps.FirstOrDefault(x => x.Value == __instance.actualClientId).Key;

        PlayerTeleportedMessage networkMessage = new() { AloeId = aloeId, PlayerId = __instance.actualClientId };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("Aloe_PlayerTeleportedMessage", NetworkManager.ServerClientId, writer);
    }
}