using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Util;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class UtilityDrivenSearch : SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter>
{
    public struct ScorerWeight
    {
        public UtilityScorer<WaxSoldierBlackboard, WaxSoldierAdapter> Scorer;
        public float Weight;
    }
    
    private readonly HashSet<GameObject> _visitedNodes;
    private readonly List<ScorerWeight> _scorerWeights;
    
    private readonly NavMeshPath _navMeshPath = new();
    
    private readonly float _searchRadius;

    public UtilityDrivenSearch(
        AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx,
        List<ScorerWeight> scorerWeights,
        float searchRadius) : base(ctx)
    {
        _scorerWeights = scorerWeights;
        _searchRadius = searchRadius;
        _visitedNodes = [];
    }

    public override void Start()
    {
        _visitedNodes.Clear();
    }

    public override bool TryGetNextSearchPosition(out Vector3 nextPosition)
    {
        // DebugShapeVisualizer.Clear(this);
        
        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        List<GameObject> nearbyNodes = GetNearbyNodes(lkp, _searchRadius);

        GameObject bestNode = null;
        float highestScore = float.MinValue;

        for (int i = 0; i < nearbyNodes.Count; i++)
        {
            GameObject node = nearbyNodes[i];
            
            // Skip nodes we've already checked
            if (_visitedNodes.Contains(node))
                continue;

            // Skip nodes we can already see
            if (LineOfSightUtil.HasLineOfSight(
                    node.transform.position,
                    context.Adapter.EyeTransform,
                    context.Blackboard.ViewWidth,
                    context.Blackboard.ViewRange,
                    2f))
                continue;

            float totalUtility = 0f;
            float totalWeight = 0f;

            for (int j = 0; j < _scorerWeights.Count; j++)
            {
                ScorerWeight scorerWeight = _scorerWeights[j];
                if (scorerWeight.Weight <= 0) continue;
                
                float score = scorerWeight.Scorer.Score(node.transform.position, null);
                
                totalUtility += score * scorerWeight.Weight;
                totalWeight += scorerWeight.Weight;
            }
            
            float finalScore = totalWeight > 0 ? totalUtility / totalWeight : 0;
            if (finalScore > highestScore)
            {
                highestScore = finalScore;
                bestNode = node;
            }
        }

        if (bestNode)
        {
            // We found our next target
            _visitedNodes.Add(bestNode);
            nextPosition = bestNode.transform.position;
            return true;
        }
        
        // No valid, non-visible, non-visited nodes found
        nextPosition = default;
        return false;
    }

    public override void Update()
    {
        
    }

    public override void Conclude()
    {
        _visitedNodes.Clear();
    }
    
    private List<GameObject> GetNearbyNodes(Vector3 position, float radius)
    {
        List<GameObject> nearbyNodes = [];
        GameObject[] nodes = context.Adapter.AssignedAINodes;

        float radiusSqr = radius * radius;
        
        for (int i = 0; i < nodes.Length; i++)
        {
            GameObject node = nodes[i];
            
            // If the node is outside of our search radius, then ignore it
            if ((position - node.transform.position).sqrMagnitude > radiusSqr) 
                continue;
            
            bool foundPath = NavMesh.CalculatePath(position, node.transform.position, context.Adapter.Agent.areaMask, _navMeshPath);
            
            // Check if we can actually path to this node
            if (!foundPath || _navMeshPath.status != NavMeshPathStatus.PathComplete)
                continue;

            float navMeshDistance = 0f;
            Vector3[] corners = _navMeshPath.corners;
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