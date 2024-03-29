using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Patches;
[HarmonyPatch(typeof(GameNetworkManager))]
internal static class GameNetworkManagerPatch
{
    internal static List<GameObject> networkPrefabsToRegister = [];

    [HarmonyPatch(nameof(GameNetworkManager.Start)), HarmonyPrefix]
    static void AddNetworkPrefabs()
    {
        foreach (GameObject prefab in networkPrefabsToRegister)
        {
            NetworkManager.Singleton.AddNetworkPrefab(prefab);
        }
        BiodiversityPlugin.Logger.LogInfo($"Succesfully registered {networkPrefabsToRegister.Count} network prefabs.");
    }
}