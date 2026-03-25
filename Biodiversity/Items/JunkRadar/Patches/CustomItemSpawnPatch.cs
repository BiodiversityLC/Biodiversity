using Biodiversity.Util;
using HarmonyLib;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class CustomItemSpawnPatch
    {
        /// <summary>
        /// Try to spawn the Junk Radar item every time SpawnScrapInLevel is called (when a moon is loading)
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "SpawnScrapInLevel")]
        public static void TrySpawnJunkRadar(RoundManager __instance)
        {
            if (!JunkRadarHandler.Instance.Config.Enabled)
            {
                return;
            }
            // Ignore non valid moons
            if (__instance == null || StartOfRound.Instance == null || StartOfRound.Instance.currentLevel == null
                || (!StartOfRound.Instance.currentLevel.planetHasTime && !StartOfRound.Instance.currentLevel.spawnEnemiesAndScrap))
            {
                return;
            }
            // Fill up the spawn list if not already filled
            if (JunkRadarHandler.Instance.Config.SpawnMoonsList.Count == 0 && !string.IsNullOrEmpty(JunkRadarHandler.Instance.Config.SpawnMoons))
            {
                foreach (string moonName in JunkRadarHandler.Instance.Config.SpawnMoons.Split(',').Select(s => s.Trim()))
                {
                    JunkRadarHandler.Instance.Config.SpawnMoonsList.Add(GetNormalizedMoonName(moonName));
                }
            }
            // Ignore moons not in the spawn list
            if (!JunkRadarHandler.Instance.Config.SpawnMoonsList.Exists(moonName => moonName == GetNormalizedMoonName(StartOfRound.Instance.currentLevel.PlanetName) || moonName == "All"))
            {
                return;
            }
            // Spawn Radar item
            if (JunkRadarItem.Instance != null)
            {
                Reload();
            }
            // Reload buried scraps
            if (__instance.IsServer)
            {
                Spawn();
            }
        }


        /// <summary>
        /// Spawn the Junk Radar item
        /// </summary>
        private static void Spawn()
        {
            if (Random.Range(0, 100) < JunkRadarHandler.Instance.Config.SpawnChance)
            {
                var spawnPosition = PositionUtils.GetRandomMoonPosition(randomizePositionRadius: 10);
                var junkRadar = Object.Instantiate(JunkRadarHandler.Instance.Assets.JunkRadarItem.spawnPrefab, spawnPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
                var radarComponent = junkRadar.GetComponent<JunkRadarItem>();
                radarComponent.fallTime = 1f;
                radarComponent.hasHitGround = true;
                radarComponent.reachedFloorTarget = true;
                radarComponent.isInFactory = false;
                radarComponent.NetworkObject.Spawn();
                radarComponent.SetBuriedStateServerRpc(radarComponent.NetworkObject, Random.Range(0, 360));
            }
        }

        /// <summary>
        /// Reload buried scraps for the current master Junk Radar
        /// </summary>
        private static void Reload()
        {
            if (JunkRadarItem.Instance.hasBeenHeld)
            {
                JunkRadarItem.Instance.InitializeBuriedScraps();
            }
        }


        #region MOON NAMES UTILITIES

        static public string GetNormalizedMoonName(string planetName)
        {
            var moonName = Regex.Replace(planetName, "^[0-9]+", string.Empty);
            if (moonName[0] == ' ' || moonName[0] == '-')
                moonName = moonName[1..];
            return moonName;
        }

        #endregion
    }
}
