using GameNetcodeStuff;

namespace Biodiversity.Util;
internal static class PlayerUtil {
    // lazy as hell :sob:
    internal static PlayerControllerB GetPlayerFromClientId(int playerClientId) {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
}
