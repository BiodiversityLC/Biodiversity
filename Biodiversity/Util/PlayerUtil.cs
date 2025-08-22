using GameNetcodeStuff;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Biodiversity.Util;
internal static class PlayerUtil 
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(ulong playerClientId) 
    {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
    
    // Used so I dont mix up `PlayerControllerB.playerClientId` and `PlayerControllerB.actualClientId`
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetClientIdFromPlayer(PlayerControllerB player)
    {
        return player.playerClientId;
    }
    
    /// <summary>
    /// Determines whether the specified player is dead.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>Returns true if the player is dead or not controlled; otherwise, false.</returns>
    internal static bool IsPlayerDead(PlayerControllerB player)
    {
        if (!player) return true; // todo: remove null check maybe? Idk if null check should be this functions responsibility
        return player.isPlayerDead || !player.isPlayerControlled;
        
    }
    
    internal static Vector3 GetVelocityOfPlayer(PlayerControllerB player)
    {
        if (player.IsOwner)
            return Vector3.Normalize(player.thisController.velocity * 100f);
        return player.timeSincePlayerMoving < 0.25 ? Vector3.Normalize((player.serverPlayerPosition - player.oldPlayerPosition) * 100f) : Vector3.zero;
    }
}
