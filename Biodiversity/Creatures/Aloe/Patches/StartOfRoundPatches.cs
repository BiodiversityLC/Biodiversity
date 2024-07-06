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
    /// It makes sure the bracken room location is set to null when the ship leaves
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
            if (AloeSharedData.Instance.PlayersMaxHealth.ContainsKey(player))
                AloeSharedData.Instance.PlayersMaxHealth.Remove(player);
            
            AloeSharedData.Instance.PlayersMaxHealth.Add(player, player.health);
        }
    }
}
