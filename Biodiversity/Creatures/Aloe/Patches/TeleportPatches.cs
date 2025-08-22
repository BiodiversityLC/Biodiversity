using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util;
using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

[CreaturePatch("Aloe")]
[HarmonyPatch(typeof(PlayerControllerB))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class TeleportPatches
{
    // Unbinds the player from the aloe if they are teleported during the kidnapping
    [HarmonyPatch("TeleportPlayer")]
    [HarmonyPostfix]
    private static void PostfixTeleportPlayer(PlayerControllerB __instance, Vector3 pos, bool withRotation = false,
        float rot = 0f, bool allowInteractTrigger = false, bool enableController = true)
    {
        if (!__instance.IsHost && !__instance.IsServer) return;
        if (!__instance) return;

        if (!AloeSharedData.Instance.IsPlayerKidnapBound(__instance)) return;

        ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(__instance);
        
        KeyValuePair<string, ulong> first = new();
        foreach (KeyValuePair<string, ulong> x in AloeSharedData.Instance.AloeBoundKidnaps)
        {
            if (x.Value != playerClientId) continue;
            
            first = x;
            break;
        }

        string aloeId = first.Key;

        PlayerTeleportedMessage networkMessage = new() { BioId = aloeId, PlayerId = playerClientId };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("Aloe_PlayerTeleportedMessage",
            NetworkManager.ServerClientId, writer);
    }
}