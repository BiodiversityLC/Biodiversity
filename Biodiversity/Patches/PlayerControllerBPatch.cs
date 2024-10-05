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
            }

            BiodiversityPlugin.Logger.LogInfo("Clearing teleport data");
            TeleporterStatus.Teleporting = false;
            TeleporterStatus.PlayerGettingTeleported = null;
            TeleporterStatus.currentTeleporter = null;


            return !cancel;
        }
    }
}
