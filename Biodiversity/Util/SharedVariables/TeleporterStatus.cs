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

        // Teleporter render fix
        public static bool? isInElevator = null;
        public static bool? isInHangarShipRoom = null;
        public static bool? isInFactory = null;




        public static PlayerControllerB PlayerGettingInverseTeleported = null;
        public static bool TeleportingInverse = true;
        public static ShipTeleporter currentTeleporterInverse = null;

        // Inverse teleporter render fix
        public static bool? isInElevatorInverse = null;
        public static bool? isInHangarShipRoomInverse = null;
        public static bool? isInFactoryInverse = null;

        // Make sure to sync setting these variables or weird shit will start to happen
        public static bool CancelTeleport = false;
        public static bool CancelInverseTeleport = false;
    }
}
