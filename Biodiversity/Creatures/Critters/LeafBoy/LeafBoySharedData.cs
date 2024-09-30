using System;
using System.Collections.Concurrent;

namespace Biodiversity.Creatures.Critters.LeafBoy;

internal class LeafBoySharedData
{
    // todo: fix the aloe 1 million different log sources and the configs

    private static readonly object Padlock = new();
    private static LeafBoySharedData _instance;

    internal static LeafBoySharedData Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Padlock)
            {
                _instance ??= new LeafBoySharedData();
            }

            return _instance;
        }
    }
    
    internal enum RoleInGroup
    {
        Leader,
        Follower
    }

    private const int MaxLeafBoysPerGroup = 8;

    // private readonly ConcurrentDictionary<LeafBoyAI, ConcurrentStack<LeafBoyAI>> _leafBoyGroups = new();
    //
    // internal RoleInGroup AssignLeafBoy(LeafBoyAI leafBoyToAssign)
    // {
    //     lock (Padlock) // Makes sure that only one LeafBoy is assigned at a time
    //     {
    //         if (_leafBoyGroups.Count == 0) // Checks if there are no groups at all
    //         {
    //             _leafBoyGroups.TryAdd(leafBoyToAssign, new ConcurrentStack<LeafBoyAI>());
    //             return RoleInGroup.Leader;
    //         }
    //
    //         foreach (KeyValuePair<LeafBoyAI, ConcurrentStack<LeafBoyAI>> pair in _leafBoyGroups)
    //         {
    //             if (pair.Value.Count < MaxLeafBoysPerGroup) // Checks if there is an empty spot in a group to join
    //             {
    //                 pair.Value.Push(leafBoyToAssign);
    //                 return RoleInGroup.Follower;
    //             }
    //         }
    //     
    //         // Assigns the LeafBoy to be the leader of a new group if there are no free groups to join
    //         _leafBoyGroups.TryAdd(leafBoyToAssign, new ConcurrentStack<LeafBoyAI>());
    //         return RoleInGroup.Leader;
    //     }
    // }

    private readonly ConcurrentDictionary<LeafBoyAI, LeafBoyGroup> _leaderToGroupMap = new();
    private readonly ConcurrentDictionary<LeafBoyAI, LeafBoyGroup> _memberToGroupMap = new();

    internal RoleInGroup AssignLeafBoy(LeafBoyAI leafBoyToAssign)
    {
        if (leafBoyToAssign == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(leafBoyToAssign)}' is null.");
        
        if (_memberToGroupMap.ContainsKey(leafBoyToAssign))
            throw new InvalidOperationException($"The given LeafBoyAI class '{nameof(leafBoyToAssign)}' is already assigned to a group.");

        foreach (LeafBoyGroup group in _leaderToGroupMap.Values)
        {
            if (group.CurrentFollowerCount >= MaxLeafBoysPerGroup) continue;
            if (!group.AddFollower(leafBoyToAssign)) continue;
            
            _memberToGroupMap.TryAdd(leafBoyToAssign, group);
            return RoleInGroup.Follower;
        }

        LeafBoyGroup newGroup = new(leafBoyToAssign);
        
        if (!_leaderToGroupMap.TryAdd(newGroup.Leader, newGroup))
            throw new InvalidOperationException($"Failed to add a new LeafBoy '{nameof(leafBoyToAssign)}' as a leader.");
        
        _memberToGroupMap.TryAdd(leafBoyToAssign, newGroup);
        return RoleInGroup.Leader;
    }

    internal bool RemoveLeafBoy(LeafBoyAI leafBoyToRemove)
    {
        if (leafBoyToRemove == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(leafBoyToRemove)}' is null.");

        if (!_memberToGroupMap.TryGetValue(leafBoyToRemove, out LeafBoyGroup group))
            return false;

        if (group.Leader.Equals(leafBoyToRemove))
        {
            LeafBoyAI newLeader = group.PromoteAnyFollowerToLeader();

            if (newLeader != null)
            {
                _leaderToGroupMap.TryRemove(leafBoyToRemove, out _);
                _leaderToGroupMap.TryAdd(newLeader, group);
                _memberToGroupMap[newLeader] = group;

                foreach (LeafBoyAI follower in group.Followers.Keys)
                {
                    _memberToGroupMap[follower] = group;
                }
            }
            else
            {
                _leaderToGroupMap.TryRemove(leafBoyToRemove, out _);
            }

            _memberToGroupMap.TryRemove(leafBoyToRemove, out _);
            return true;
        }

        bool removed = group.RemoveFollower(leafBoyToRemove);
        if (removed)
        {
            _memberToGroupMap.TryRemove(leafBoyToRemove, out _);
            return true;
        }

        return false;
    }

    internal LeafBoyGroup GetGroup(LeafBoyAI leafBoy)
    {
        if (leafBoy == null)
            throw new ArgumentNullException($"The given LeafBoyAI class '{nameof(leafBoy)}' is null.");

        _memberToGroupMap.TryGetValue(leafBoy, out LeafBoyGroup group);
        return group;
    }

    internal RoleInGroup? GetRoleInGroup(LeafBoyAI leafBoy)
    {
        LeafBoyGroup group = GetGroup(leafBoy);
        if (group == null)
            return null;

        return group.Leader.Equals(leafBoy) ? RoleInGroup.Leader : RoleInGroup.Follower;
    }
}