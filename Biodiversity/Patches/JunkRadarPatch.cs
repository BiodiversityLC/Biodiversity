using Biodiversity.Items.JunkRadar;
using HarmonyLib;

namespace Biodiversity.Patches
{
    [HarmonyPatch]
    internal class JunkRadarPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemCharger), "chargeItemDelayed")]
        public static void ChargingAnimationOnOwnerPatch(ItemCharger __instance, GrabbableObject itemToCharge)
        {
            if (itemToCharge != null && itemToCharge is JunkRadarItem junkRadar)
            {
                junkRadar.isBeingCharged = true;
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemCharger), "PlayChargeItemEffectClientRpc")]
        public static void ChargingAnimationOnNonOwnerPatch(ItemCharger __instance, int playerChargingItem)
        {
            if (GameNetworkManager.Instance.localPlayerController != null && (int)GameNetworkManager.Instance.localPlayerController.playerClientId != playerChargingItem)
            {
                var player = StartOfRound.Instance.allPlayerScripts[playerChargingItem];
                if (player != null && player.currentlyHeldObjectServer != null && player.currentlyHeldObjectServer is JunkRadarItem junkRadar)
                {
                    junkRadar.isBeingCharged = true;
                }
            }
        }
    }
}
