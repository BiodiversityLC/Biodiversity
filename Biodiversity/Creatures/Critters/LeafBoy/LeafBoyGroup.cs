using Biodiversity.Creatures.Critters.LeafBoy.Formations;
using Biodiversity.Util.Types;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.LeafBoy;

internal class LeafBoyGroup(LeafBoyAI leader, FormationType defaultFormationType)
{
    public FormationType DefaultFormationType = defaultFormationType;
    public LeafBoyAI Leader { get; private set; } = leader ?? throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(leader)}' is null.");
    
    public List<LeafBoyAI> Followers { get; } = [];
    
    public readonly NullableObject<IFormation> CurrentFormation = new();

    public readonly List<Vector3> LeaderPathHistory = [];
    
    public int MaxFollowers { get; private set; } = 8;
    public float EscortRowSpacing { get; private set; } = 3F;

    public void UpdateFollowerPositions()
    {
        //todo: leader size thing
        const float LEADER_SIZE_REPLACE_ME = 1f;
        
        if (!CurrentFormation.IsNotNull || Followers.Count == 0) return;

        for (int i = Followers.Count - 1; i >= 0; i--)
        {
            LeafBoyAI follower = Followers[i];
            if (follower.isEnemyDead)
            {
                RemoveFollower(follower);
                continue;
            }

            if ((Leader.transform.position - follower.transform.position).magnitude < EscortRowSpacing) continue;
            
            Vector3 targetPosition = CurrentFormation.Value.GetFollowerTargetPosition(LeaderPathHistory, i, LEADER_SIZE_REPLACE_ME);
            follower.SetDestinationToPosition(targetPosition);
        }
    }
    
    public bool AddFollower(LeafBoyAI follower)
    {
        if (follower == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(follower)}' is null.");

        if (Followers.Count >= MaxFollowers) return false;
        Followers.Add(follower);
        follower.Group.Value = this;
        return true;
    }

    public bool RemoveFollower(LeafBoyAI follower)
    {
        if (follower == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(follower)}' is null.");
        
        if (!Followers.Remove(follower)) return false;
        follower.Group.Value = null;
        return true;

    }
    
    public LeafBoyAI PromoteAnyFollowerToLeader()
    {
        foreach (LeafBoyAI follower in Followers)
        {
            if (!Followers.Remove(follower)) continue;

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
