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
                BiodiversityPlugin.Logger.LogInfo("Recording Teleport data.");
                TeleporterStatus.Teleporting = true;
                TeleporterStatus.PlayerGettingTeleported = StartOfRound.Instance.mapScreen.targetedPlayer;
                TeleporterStatus.currentTeleporter = __instance;
    }
        }
    }
}
