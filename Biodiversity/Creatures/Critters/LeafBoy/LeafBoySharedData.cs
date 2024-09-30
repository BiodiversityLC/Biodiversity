using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Biodiversity.Creatures.Critters.LeafBoy;

internal class LeafBoySharedData
{
    // todo: fix the aloe 1 million different log sources

    private static readonly object Padlock = new();
    private static LeafBoySharedData _instance;

    public static LeafBoySharedData Instance
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
    
    internal enum AssignmentResult
    {
        Leader,
        Follower
    }

    private const int MaxLeafBoysPerGroup = 8;

    private readonly ConcurrentDictionary<LeafBoyAI, ConcurrentStack<LeafBoyAI>> _leafBoyGroups = new();

    public AssignmentResult AssignLeafBoy(LeafBoyAI leafBoyToAssign)
    {
        if (_leafBoyGroups.Count == 0) // Checks if there are no groups at all
        {
            _leafBoyGroups.TryAdd(leafBoyToAssign, new ConcurrentStack<LeafBoyAI>());
            return AssignmentResult.Leader;
        }

        foreach (KeyValuePair<LeafBoyAI, ConcurrentStack<LeafBoyAI>> pair in _leafBoyGroups)
        {
            if (pair.Value.Count < MaxLeafBoysPerGroup) // Checks if there is an empty group but with a leader
            {
                pair.Value.Push(leafBoyToAssign);
            }
        }
        //todo: finish this
    }
}