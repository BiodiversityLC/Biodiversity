using System;
using System.Collections.Generic;
using System.Text;
using Biodiversity.Util.SharedVariables;
using HarmonyLib;

namespace Biodiversity.Patches
{
    [HarmonyPatch(typeof(ShipTeleporter))]
    internal static class ShipTeleporterPatch
    {
        [HarmonyPatch(nameof(ShipTeleporter.PressTeleportButtonClientRpc)), HarmonyPrefix]
        private static void ButtonPressed(ShipTeleporter __instance)
        {
            if (!__instance.isInverseTeleporter)
            {
                BiodiversityPlugin.Logger.LogInfo("Recording teleport data.");
                TeleporterStatus.Teleporting = true;
                TeleporterStatus.PlayerGettingTeleported = StartOfRound.Instance.mapScreen.targetedPlayer;
                TeleporterStatus.currentTeleporter = __instance;

                TeleporterStatus.isInElevator = TeleporterStatus.PlayerGettingTeleported.isInElevator;
                TeleporterStatus.isInHangarShipRoom = TeleporterStatus.PlayerGettingTeleported.isInHangarShipRoom;
                TeleporterStatus.isInFactory = TeleporterStatus.PlayerGettingTeleported.isInsideFactory;
            }
            else
            {
                BiodiversityPlugin.Logger.LogInfo("Recording inverse teleport data.");
                TeleporterStatus.TeleportingInverse = true;
                TeleporterStatus.PlayerGettingInverseTeleported = StartOfRound.Instance.mapScreen.targetedPlayer;
                TeleporterStatus.currentTeleporterInverse = __instance;

                TeleporterStatus.isInElevatorInverse = TeleporterStatus.PlayerGettingInverseTeleported.isInElevator;
                TeleporterStatus.isInHangarShipRoomInverse = TeleporterStatus.PlayerGettingInverseTeleported.isInHangarShipRoom;
                TeleporterStatus.isInFactoryInverse = TeleporterStatus.PlayerGettingInverseTeleported.isInsideFactory;
            }
        }
    }
}
