namespace Biodiversity.Items.JunkRadar.Patches
{
    //[HarmonyPatch]
    internal class OgopogoTrophyPatches
    {
        /*
        private static bool itemNeedsToBeAllowed = false;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemCharger), "Update")]
        public static void AllowChargingOgoTrophyPatch(ref ItemCharger __instance)
        {
            if (__instance.updateInterval == 0f)
            {
                if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
                {
                    GrabbableObject item = GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer;
                    itemNeedsToBeAllowed = item != null && item is OgopogoTrophyItem;
                    __instance.triggerScript.twoHandedItemAllowed = itemNeedsToBeAllowed;
                    BiodiversityPlugin.Logger.LogError(itemNeedsToBeAllowed);
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemCharger), "ChargeItem")]
        public static void ResetChargingOgoTrophyPatch(ref ItemCharger __instance)
        {
            if (GameNetworkManager.Instance != null && GameNetworkManager.Instance.localPlayerController != null)
            {
                GrabbableObject item = GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer;
                if (itemNeedsToBeAllowed && item != null && item is OgopogoTrophyItem)
                {
                    __instance.triggerScript.twoHandedItemAllowed = false;
                    itemNeedsToBeAllowed = false;
                    BiodiversityPlugin.Logger.LogError("reset");
                }
            }
        }
        */
    }
}
