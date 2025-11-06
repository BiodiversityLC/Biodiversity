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
            if (__instance == null || !__instance.IsServer)
            {
                return;
            }
            if (JunkRadarItem.Instance == null)
            {
                Spawn();
            }
            else
            {
                Reload();
            }
        }


        private static void Spawn()
        {
            var spawnPosition = PositionUtils.GetRandomMoonPosition(randomizePositionRadius: 30);
            var spawnRotation = Quaternion.Euler(new Vector3(0f, Random.Range(0f, 360f), 0f));
            var junkRadar = Object.Instantiate(JunkRadarHandler.Instance.Assets.JunkRadarItem.spawnPrefab, spawnPosition, spawnRotation, RoundManager.Instance.spawnedScrapContainer);
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
                JunkRadarItem.Instance.EnabledBuriedScraps();
            }
        }
    }
}
