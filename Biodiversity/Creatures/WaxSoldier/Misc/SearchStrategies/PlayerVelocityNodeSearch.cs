using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Vector3 = UnityEngine.Vector3;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class PlayerVelocityNodeSearch(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx) : SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter>(ctx)
{
    private Queue<Vector3> searchQueue;

    public override void Start()
    {
        searchQueue ??= new Queue<Vector3>();

        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        Vector3 lkv = context.Blackboard.LastKnownPlayerVelocity.normalized;
        
        List<GameObject> nearbyNodes = GetNearbyNodes(lkp, 25f);

        IEnumerable<Vector3> sortedByDirectionNodes = nearbyNodes.Select(node =>
            {
                Vector3 directionToNode = (node.transform.position - lkp).normalized;
                float alignmentScore = Vector3.Dot(lkv, directionToNode);
                return new { Node = node, Score = alignmentScore };
            })
            .Where(x => x.Score > 0.1f) // Very basic heuristic: don't bother considering nodes that are in the opposite direction to where the player is going
            .OrderByDescending(x => x.Score) // We want to search the nodes that the player is most likely near to based on where we last saw them going
            .Select(x => x.Node.transform.position);

        searchQueue.Enqueue(lkp); // Go to the player's last known position first
        foreach (Vector3 position in sortedByDirectionNodes)
        {
            searchQueue.Enqueue(position);
        }
    }

    public override bool TryGetNextSearchPosition(out Vector3 nextPosition)
    {
        return searchQueue.TryDequeue(out nextPosition);
    }

    public override void Update()
    {
        
    }

    public override void Conclude()
    {
        searchQueue?.Clear();
    }

    private List<GameObject> GetNearbyNodes(Vector3 position, float radius)
    {
        List<GameObject> nearbyNodes = [];
        GameObject[] nodes = context.Adapter.AssignedAINodes;

        NavMeshPath path = new();
        
        for (int i = 0; i < nodes.Length; i++)
        {
            GameObject node = nodes[i];
            
            if (Vector3.Distance(position, node.transform.position) > radius) continue;
            
            bool foundPath = NavMesh.CalculatePath(position, node.transform.position, context.Adapter.Agent.areaMask, path);
            if (!foundPath || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float navMeshDistance = 0f;
            Vector3[] corners = path.corners;
            for (int j = 1; j < corners.Length; j++)
            {
                navMeshDistance += Vector3.Distance(corners[j - 1], corners[j]);
                if (navMeshDistance > radius) break;
            }

            if (navMeshDistance <= radius)
                nearbyNodes.Add(node);
        }
        
        return nearbyNodes;
    }
}