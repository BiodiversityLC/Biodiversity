using Biodiversity.Items.JunkRadar.BuriedScrap;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class InstanceManagerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), "SetShipReadyToLand")]
        public static void SetShipReadyToLandPatch(StartOfRound __instance)
        {
            if (JunkRadarHandler.Instance.Config.Enabled)
            {
                RestoreJunkRadarOriginalInstance(shouldBeHeld: true);
            }
        }


        public static void RestoreJunkRadarOriginalInstance(bool shouldBeHeld = false, string idToIgnore = null)
        {
            if (JunkRadarItem.Instance != null)
            {
                return;
            }
            List<JunkRadarItem> items = UnityEngine.Object.FindObjectsOfType<JunkRadarItem>().ToList().FindAll(
                item => item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned && (idToIgnore == null || item.BioId != idToIgnore) && (!shouldBeHeld || item.hasBeenHeld));
            if (items.Count > 0)
            {
                items[0].LoadItemSaveData(1);
                BiodiversityPlugin.Logger.LogInfo($"Restored Junk Radar original instance to BiodiverseItem {items[0].BioId}");
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), "EndOfGame")]
        public static void EndOfGamePatch()
        {
            if (JunkRadarHandler.Instance.Config.Enabled && JunkRadarItem.Instance != null && JunkRadarItem.Instance.IsServer)
            {
                DespawnBuriedScraps(JunkRadarItem.Instance.detectedBuriedScraps);
            }
        }


        public static void DespawnBuriedScraps(List<BuriedScrapObject> allBuriedScraps)
        {
            foreach (BuriedScrapObject buriedScrap in allBuriedScraps)
            {
                if (buriedScrap != null && buriedScrap.gameObject != null)
                {
                    NetworkObject component = buriedScrap.gameObject.GetComponent<NetworkObject>();
                    if (component != null && component.IsSpawned)
                    {
                        component.Despawn();
                    }
                }
            }
        }
    }
}
