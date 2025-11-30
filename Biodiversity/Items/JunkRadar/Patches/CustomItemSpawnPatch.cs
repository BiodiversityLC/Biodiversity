using Biodiversity.Util;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class CustomItemSpawnPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        public static void TrySpawnJunkRadar(RoundManager __instance)
        {
            if (__instance == null)
            {
                return;
            }
            if (JunkRadarItem.Instance != null)
            {
                Reload();
            }
            if (__instance.IsServer)
            {
                Spawn();
            }
        }


        private static void Spawn()
        {
            var spawnPosition = PositionUtils.GetRandomMoonPosition(randomizePositionRadius: 30);
            var junkRadar = Object.Instantiate(JunkRadarHandler.Instance.Assets.JunkRadarItem.spawnPrefab, spawnPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
            var radarComponent = junkRadar.GetComponent<JunkRadarItem>();
            radarComponent.fallTime = 1f;
            radarComponent.hasHitGround = true;
            radarComponent.reachedFloorTarget = true;
            radarComponent.isInFactory = false;
            radarComponent.NetworkObject.Spawn();
        }

        private static void Reload()
        {
            if (JunkRadarItem.Instance.hasBeenHeld)
            {
                JunkRadarItem.Instance.InitializeBuriedScraps();
            }
        }
    }
}
