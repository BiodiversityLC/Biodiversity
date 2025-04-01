using HarmonyLib;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Patches;
[HarmonyPatch(typeof(GameNetworkManager))]
internal static class GameNetworkManagerPatch 
{
    internal static readonly HashSet<GameObject> NetworkPrefabsToRegister = [];

    [HarmonyPatch(nameof(GameNetworkManager.Start)), HarmonyPrefix]
    private static void AddNetworkPrefabs() 
    {
        BiodiversityPlugin.Instance.FinishLoading();

        if (NetworkPrefabsToRegister.Count > 0)
        {
            BiodiversityPlugin.LogVerbose($"Registering {NetworkPrefabsToRegister.Count} network prefabs...");
            
            foreach (GameObject prefab in NetworkPrefabsToRegister)
            {
                if (prefab == null) continue;
                NetworkManager.Singleton.AddNetworkPrefab(prefab);
                BiodiversityPlugin.LogVerbose($"Registered {prefab.name} as a network prefab.");
            }

            BiodiversityPlugin.LogVerbose($"Successfully registered {NetworkPrefabsToRegister.Count} network prefabs.");
        }

        NetworkPrefabsToRegister.Clear();
    }
}
