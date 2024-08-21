using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// A class of patches for the StartOfRound class.
/// </summary>
[HarmonyPatch(typeof(StartOfRound))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class StartOfRoundPatch
{
    /// <summary>
    /// It makes sure to clear the round specific data when the round is finished.
    /// It is called on all clients.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(StartOfRound.ShipLeave))]
    [HarmonyPostfix]
    private static void ResetData(StartOfRound __instance)
    {
        AloeSharedData.Instance.FlushDictionaries();
    }

    /// <summary>
    /// Gets the max health of all the players
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(StartOfRound.StartGame))]
    [HarmonyPostfix]
    private static void GetAllPlayersMaxHealth(StartOfRound __instance)
    {
        foreach (PlayerControllerB player in __instance.allPlayerScripts)
        {
            AloeSharedData.Instance.SetPlayerMaxHealth(player, player.health);
        }
    }
}
