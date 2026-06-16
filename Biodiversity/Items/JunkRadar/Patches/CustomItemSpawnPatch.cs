using Biodiversity.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class CustomItemSpawnPatch
    {
        /// <summary>
        /// NavMesh areas to avoid while position sampling when spawning the radar item or buried items.
        /// Excluded areas: NotWalkable, PlayerShip, Climb and Water
        /// </summary>
        public static readonly int navMeshMask = NavMesh.AllAreas & ~((1 << 1) | (1 << 4) | (1 << 7) | (1 << 12));

        /// <summary>
        /// A subset of vanilla outsideAIDryNodes with excluded positions for the radar item and buried items to spawn from,
        /// will be calculated at runtime
        /// </summary>
        public static GameObject[] radarNodes;


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
                    if (!string.IsNullOrEmpty(moonName))
                    {
                        JunkRadarHandler.Instance.Config.SpawnMoonsList.Add(GetNormalizedMoonName(moonName));
                    }
                }
            }
            // Ignore moons not in the spawn list
            if (!JunkRadarHandler.Instance.Config.SpawnMoonsList.Exists(moonName => moonName == GetNormalizedMoonName(StartOfRound.Instance.currentLevel.PlanetName) || moonName == "All"))
            {
                return;
            }
            // Trigger radarNodes calculation
            radarNodes = null;
            RoundManager.Instance.GetOutsideAINodes();
            // Reload buried scraps
            if (JunkRadarItem.Instance != null)
            {
                Reload();
            }
            // Spawn Radar item
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
                Vector3 spawnPosition = PositionUtils.GetRandomMoonPosition(radarNodes, randomRadius: 10, navMeshMask);
                GameObject junkRadar = Object.Instantiate(JunkRadarHandler.Instance.Assets.JunkRadarItem.spawnPrefab, spawnPosition, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
                JunkRadarItem radarComponent = junkRadar.GetComponent<JunkRadarItem>();
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


        /// <summary>
        /// Calculate radarNodes based of outsideAIDryNodes whenever GetOutsideAINodes() is run.
        /// radarNodes is an array of outsideAIDryNodes with some excluded tags
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "GetOutsideAINodes")]
        public static void CalculateRadarNodes(RoundManager __instance)
        {
            if (__instance.outsideAIDryNodes == null || __instance.outsideAIDryNodes.Length == 0 || (radarNodes != null && radarNodes.Length != 0))
            {
                return;
            }

            List<GameObject> radarNodesList = [];
            int groundMask = (1 << LayerMask.NameToLayer("Room")) | (1 << LayerMask.NameToLayer("Colliders"));

            foreach (GameObject node in __instance.outsideAIDryNodes)
            {
                Vector3 originPos = node.transform.position + Vector3.up * 10f;

                if (Physics.Raycast(originPos, Vector3.down, out RaycastHit hit, 50f, groundMask))
                {
                    string tag = hit.collider.tag;
                    // only includes nodes that are not listed here
                    if (tag != "Wood" && tag != "Catwalk" && tag != "Concrete")
                    {
                        radarNodesList.Add(node);
                    }
                }
            }
            radarNodes = radarNodesList.ToArray();
        }


        #region MOON NAMES UTILITIES

        static public string GetNormalizedMoonName(string planetName)
        {
            string moonName = Regex.Replace(planetName, "^[0-9]+", string.Empty);
            if (moonName[0] == ' ' || moonName[0] == '-')
                moonName = moonName[1..];
            return moonName;
        }

        #endregion
    }
}
