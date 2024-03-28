using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Util;
internal static class PlayerUtil {
    // lazy as hell :sob:
    internal static PlayerControllerB GetPlayerFromClientId(int playerClientId) {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
}
