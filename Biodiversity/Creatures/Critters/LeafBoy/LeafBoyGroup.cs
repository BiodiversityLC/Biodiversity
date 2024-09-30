using System;
using System.Collections.Concurrent;

namespace Biodiversity.Creatures.Critters.LeafBoy;

internal class LeafBoyGroup(LeafBoyAI leader)
{
    public LeafBoyAI Leader { get; private set; } = leader ?? throw new ArgumentNullException(
                                                        $"The given LeafBoyAI class '{nameof(leader)}' is null.");

    public ConcurrentDictionary<LeafBoyAI, byte> Followers { get; } = new();

    public int MaxFollowers { get; private set; } = 8;

    public bool AddFollower(LeafBoyAI follower)
    {
        if (follower == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(follower)}' is null.");

        return Followers.Count < MaxFollowers && Followers.TryAdd(follower, 0);
    }

    public bool RemoveFollower(LeafBoyAI follower)
    {
        if (follower == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(follower)}' is null.");

        return Followers.TryRemove(follower, out _);
    }

    public LeafBoyAI PromoteAnyFollowerToLeader()
    {
        foreach (LeafBoyAI follower in Followers.Keys)
        {
            if (!Followers.TryRemove(follower, out _)) continue;

            Leader = follower;
            return Leader;
        }

        return null;
    }

    /// <summary>
    /// Gets the current number of followers.
    /// </summary>
    public int CurrentFollowerCount => Followers.Count;
}