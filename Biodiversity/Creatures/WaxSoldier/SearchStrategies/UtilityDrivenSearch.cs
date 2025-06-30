using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class UtilityDrivenSearch(
    AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx,
    List<UtilityDrivenSearch.ScorerWeight> scorerWeights,
    float _searchRadius
    ) : SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter>(ctx)
{
    public struct ScorerWeight
    {
        public UtilityScorer<WaxSoldierBlackboard, WaxSoldierAdapter> Scorer;
        public float Weight;
    }
    
    private Queue<Vector3> searchQueue;

    public override void Start()
    {
        searchQueue ??= new Queue<Vector3>();

        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        List<GameObject> nearbyNodes = GetNearbyNodes(lkp, _searchRadius);

        IEnumerable<Vector3> scoredNodes = nearbyNodes.Select(node =>
            {
                float totalUtility = 0f;
                float totalWeight = 0f;

                for (int i = 0; i < scorerWeights.Count; i++)
                {
                    ScorerWeight scorerWeight = scorerWeights[i];
                    if (scorerWeight.Weight <= 0) continue;

                    float score = scorerWeight.Scorer.Score(node.transform.position);
                    totalUtility += score * scorerWeight.Weight;
                    totalWeight += scorerWeight.Weight;
                }

                float finalScore = totalWeight > 0 ? totalUtility / totalWeight : 0;
                return new { Node = node, Score = finalScore };
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Node.transform.position);
        
        searchQueue.Enqueue(RoundManager.Instance.GetNavMeshPosition(lkp, RoundManager.Instance.navHit, -1f));
        foreach (Vector3 position in scoredNodes)
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