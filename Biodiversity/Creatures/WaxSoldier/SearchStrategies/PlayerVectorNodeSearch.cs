using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class PlayerVectorNodeSearch : ISearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter>
{
    public string StrategyName => "Player Vector Priority Node Search";

    private AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> context;

    private Queue<Vector3> searchQueue;

    public void Initialize(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        context = ctx;
        searchQueue = new Queue<Vector3>();

        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        Vector3 lkv = context.Blackboard.LastKnowPlayerVelocity.normalized;

        // todo: If the lkp is too far, we should prob not bother searching
        List<GameObject> nearbyNodes = GetNearbyNodes(lkp, 25f);

        IEnumerable<Vector3> sortedNodes = nearbyNodes.Select(node =>
            {
                Vector3 directionToNode = (node.transform.position - lkp).normalized;
                float alignmentScore = Vector3.Dot(lkv, directionToNode);
                return new { Node = node, Score = alignmentScore };
            })
            .Where(x => x.Score > 0f) // Very basic (and temp) heuristic: don't bother considering nodes that are in the opposite direction to where the player is going
            .OrderByDescending(x => x.Score) // We want to search the nodes that the player is most likely near to based on where we last saw them going
            .Select(x => x.Node.transform.position);

        foreach (Vector3 position in sortedNodes)
        {
            searchQueue.Enqueue(position);
        }
    }

    public bool TryGetNextSearchPosition(out Vector3 nextPosition)
    {
        return searchQueue.TryDequeue(out nextPosition);
    }

    public void Update()
    {
        
    }

    public void Conclude()
    {
        searchQueue?.Clear();
    }

    private List<GameObject> GetNearbyNodes(Vector3 position, float radius)
    {
        return [];
    }
}