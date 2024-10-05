using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Util.SharedVariables
{
    public static class TeleporterStatus
    {
        public static PlayerControllerB PlayerGettingTeleported = null;
        public static bool Teleporting = true;
        public static ShipTeleporter currentTeleporter = null;

        // Make sure to sync setting this variable or weird shit will start to happen
        public static bool CancelTeleport = false;
    }
}
