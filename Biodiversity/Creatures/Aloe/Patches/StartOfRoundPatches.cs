using Biodiversity.Core.Attributes;
using System.Diagnostics.CodeAnalysis;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// A class of patches for the StartOfRound class.
/// </summary>
[CreaturePatch("Aloe")]
[HarmonyPatch(typeof(StartOfRound))]
internal static class StartOfRoundPatch
{
    /// <summary>
    /// It makes sure to clear the round specific data when the round is finished.
    /// It is called on all clients.
    /// </summary>
    [HarmonyPatch(nameof(StartOfRound.ShipLeave))]
    [HarmonyPostfix]
    private static void ResetData(StartOfRound __instance)
    {
        AloeSharedData.Instance.FlushDictionaries();
    }

    /// <summary>
    /// Gets the max health of all the players
    /// </summary>
    [HarmonyPatch(nameof(StartOfRound.StartGame))]
    [HarmonyPostfix]
    private static void GetAllPlayersMaxHealth(StartOfRound __instance)
    {
        for (int i = 0; i < __instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = __instance.allPlayerScripts[i];
            if (player)
            {
                AloeSharedData.Instance.SetPlayerMaxHealth(player, player.health);
            }
        }
    }
}