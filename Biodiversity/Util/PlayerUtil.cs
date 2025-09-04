using Biodiversity.Behaviours.Player;
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
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(int playerClientId) 
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
    /// <param name="player">The player to check if dead.</param>
    /// <returns>Returns <c>true</c> if the player is dead or not controlled; otherwise, <c>false</c>.</returns>
    internal static bool IsPlayerDead(PlayerControllerB player)
    {
        if (!player) return true; // todo: remove null check maybe? Idk if null check should be this functions responsibility
        return player.isPlayerDead || !player.isPlayerControlled;
        
    }
    
    internal static Vector3 GetVelocityOfPlayer(PlayerControllerB player)
    {
        if (player.IsOwner)
        {
            if (Time.deltaTime > 0f)
            {
                // Return the true velocity
                return player.thisController.velocity / Time.deltaTime;
            }

            return Vector3.zero;
        }

        if (player.TryGetComponent(out PlayerVelocityTracker playerVelocityTracker))
        {
            return playerVelocityTracker.Velocity;
        }
        
        BiodiversityPlugin.LogVerbose($"Player {player.playerUsername} is missing a PlayerVelocityTracker component.");
        return Vector3.zero;
    }
}
