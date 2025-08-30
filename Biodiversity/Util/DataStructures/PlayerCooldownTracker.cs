using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;

namespace Biodiversity.Util.DataStructures;

public class PlayerCooldownTracker
{
    private readonly Dictionary<ulong, double> _cooldowns = new();

    /// <summary>
    /// Checks if a specific player is currently on cooldown.
    /// This method also automatically cleans up expired cooldowns.
    /// </summary>
    /// <param name="playerClientId">The client ID of the player.</param>
    /// <returns>True if the player is on cooldown, otherwise false.</returns>
    public bool IsOnCooldown(ulong playerClientId)
    {
        // Attempt to get the expiration time for the given player
        if (_cooldowns.TryGetValue(playerClientId, out double expiresAt))
        {
            // If the current server time is less than the expiration time, they are on cooldown
            if (NetworkManager.Singleton.ServerTime.Time < expiresAt)
            {
                return true;
            }
            
            // Otherwise, the cooldown has expired and we remove it from the dictionary for cleanup
            _cooldowns.Remove(playerClientId);
        }
        
        // If the player was not found or their cooldown expired, they are not on cooldown
        return false;
    }

    /// <summary>
    /// Checks if a specific player is currently on cooldown.
    /// This method also automatically cleans up expired cooldowns.
    /// </summary>
    /// <param name="player">The player object.</param>
    /// <returns>True if the player is on cooldown, otherwise false.</returns>
    public bool IsOnCooldown(PlayerControllerB player)
    {
        return IsOnCooldown(PlayerUtil.GetClientIdFromPlayer(player));
    }

    /// <summary>
    /// Starts or restarts a cooldown for a specific player.
    /// This should be called on the server. If you need to sync this to clients,
    /// the server should call this method and then send an RPC to clients to call it as well.
    /// </summary>
    /// <param name="playerClientId">The client ID of the player.</param>
    /// <param name="duration">The duration of the cooldown in seconds.</param>
    public void Start(ulong playerClientId, float duration)
    {
        if (duration <= 0)
        {
            _cooldowns.Remove(playerClientId);
            return;
        }
        
        // Record the time when the cooldown will expire
        _cooldowns[playerClientId] = NetworkManager.Singleton.ServerTime.Time + duration;
    }
    
    /// <summary>
    /// Starts or restarts a cooldown for a specific player.
    /// This should be called on the server. If you need to sync this to clients,
    /// the server should call this method and then send an RPC to clients to call it as well.
    /// </summary>
    /// <param name="player">The player object.</param>
    /// <param name="duration">The duration of the cooldown in seconds.</param>
    public void Start(PlayerControllerB player, float duration)
    {
        Start(PlayerUtil.GetClientIdFromPlayer(player), duration);
    }

    /// <summary>
    /// Clears all active cooldowns.
    /// </summary>
    public void Clear()
    {
        _cooldowns.Clear();
    }
}