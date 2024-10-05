using System;
using System.Collections.Generic;
using System.Text;
using Biodiversity.Util.SharedVariables;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal static class PlayerControllerBPatch
    {
        [HarmonyPatch(nameof(PlayerControllerB.TeleportPlayer)), HarmonyPrefix]
        private static bool TeleportCalled(PlayerControllerB __instance, Vector3 pos, bool withRotation = false, float rot = 0f, bool allowInteractTrigger = false, bool enableController = true)
        {
            bool cancel = false;

            if (TeleporterStatus.CancelTeleport == true && TeleporterStatus.Teleporting && TeleporterStatus.PlayerGettingTeleported == __instance)
            {
                cancel = true;
                TeleporterStatus.CancelTeleport = false;

                __instance.isInElevator = TeleporterStatus.isInElevator.Value;
                __instance.isInHangarShipRoom = TeleporterStatus.isInHangarShipRoom.Value;
                __instance.isInsideFactory = TeleporterStatus.isInFactory.Value;
            }

            if (TeleporterStatus.CancelInverseTeleport == true && TeleporterStatus.TeleportingInverse && TeleporterStatus.PlayerGettingInverseTeleported == __instance)
            {
                cancel = true;
                TeleporterStatus.CancelInverseTeleport = false;

                __instance.isInElevator = TeleporterStatus.isInElevatorInverse.Value;
                __instance.isInHangarShipRoom = TeleporterStatus.isInHangarShipRoomInverse.Value;
                __instance.isInsideFactory = TeleporterStatus.isInFactoryInverse.Value;
            }

            if (TeleporterStatus.Teleporting && TeleporterStatus.PlayerGettingTeleported == __instance)
            {
                BiodiversityPlugin.Logger.LogInfo("Clearing teleport data");
                TeleporterStatus.Teleporting = false;
                TeleporterStatus.PlayerGettingTeleported = null;
                TeleporterStatus.currentTeleporter = null;

                TeleporterStatus.isInElevator = null;
                TeleporterStatus.isInHangarShipRoom = null;
                TeleporterStatus.isInFactory = null;
            }

            if (TeleporterStatus.TeleportingInverse && TeleporterStatus.PlayerGettingInverseTeleported == __instance)
            {
                BiodiversityPlugin.Logger.LogInfo("Clearing inverse teleport data");
                TeleporterStatus.TeleportingInverse = false;
                TeleporterStatus.PlayerGettingInverseTeleported = null;
                TeleporterStatus.currentTeleporterInverse = null;

                TeleporterStatus.isInElevatorInverse = null;
                TeleporterStatus.isInHangarShipRoomInverse = null;
                TeleporterStatus.isInFactoryInverse = null;
            }


            return !cancel;
        }
    }
}
