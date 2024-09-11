using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Patches;
[HarmonyPatch(typeof(GameNetworkManager))]
internal static class GameNetworkManagerPatch {
    internal static readonly List<GameObject> NetworkPrefabsToRegister = [];

    [HarmonyPatch(nameof(GameNetworkManager.Start)), HarmonyPrefix]
    private static void AddNetworkPrefabs() {
        BiodiversityPlugin.Instance.FinishLoading();
        
        foreach(GameObject prefab in NetworkPrefabsToRegister) {
            NetworkManager.Singleton.AddNetworkPrefab(prefab);
            BiodiversityPlugin.Logger.LogDebug($"Registered {prefab.name} as a network prefab.");
        }
        BiodiversityPlugin.Logger.LogDebug($"Succesfully registered {NetworkPrefabsToRegister.Count} network prefabs.");
    }
}
