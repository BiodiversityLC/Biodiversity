using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Behaviours.Player;

[HarmonyPatch(typeof(StartOfRound))]
internal static class PlayerVelocityPatch
{
    [HarmonyPatch(nameof(StartOfRound.StartGame))]
    [HarmonyPostfix]
    private static void AddVelocityComponent(StartOfRound __instance)
    {
        for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = __instance.allPlayerScripts[i];
            if (!player) continue;
            
            if (!player.gameObject.TryGetComponent(out PlayerVelocityTracker _))
            {
                player.gameObject.AddComponent<PlayerVelocityTracker>();
            }
        }
    }
}