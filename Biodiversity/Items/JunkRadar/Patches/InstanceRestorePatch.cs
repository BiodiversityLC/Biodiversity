using HarmonyLib;
using System.Linq;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class InstanceRestorePatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StartOfRound), "SetShipReadyToLand")]
        public static void SetShipReadyToLandPatch(StartOfRound __instance)
        {
            if (JunkRadarItem.Instance == null)
            {
                var items = UnityEngine.Object.FindObjectsOfType<JunkRadarItem>().ToList().FindAll(
                    item => item != null && item.NetworkObject != null && item.NetworkObject.IsSpawned && item.hasBeenHeld);
                if (items.Count > 0)
                {
                    items[0].LoadItemSaveData(1);
                    BiodiversityPlugin.Logger.LogInfo($"Restored Junk Radar original instance to BiodiverseItem {items[0].BioId}");
                }
            }
        }
    }
}
