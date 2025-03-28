﻿using GameNetcodeStuff;
using System.Runtime.CompilerServices;

namespace Biodiversity.Util;
internal static class PlayerUtil 
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(int playerClientId) 
    {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
    
    /// <summary>
    /// Determines whether the specified player is dead.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>Returns true if the player is dead or not controlled; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPlayerDead(PlayerControllerB player)
    {
        if (player == null) return true;
        return player.isPlayerDead || !player.isPlayerControlled;
    }
}
