using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class DirectionAlignmentScorer(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    : UtilityScorer<WaxSoldierBlackboard, WaxSoldierAdapter>(ctx)
{
    public override float Score(Vector3 candidatePosition)
    {
        Vector3 lkp = context.Blackboard.LastKnownPlayerPosition;
        Vector3 lkv = context.Blackboard.LastKnownPlayerVelocity.normalized;

        if (lkv == Vector3.zero) return 0.5f; // Neutral score if the player was stationary

        Vector3 directionToNode = (candidatePosition - lkp).normalized;
        float dot = Vector3.Dot(directionToNode, lkv);
        if (dot < 0.1f) return 0f;
        
        return (dot + 1.0f) / 2.0f; // The dot product's range is [-1, 1]; we need it to be [0, 1] 
    }
}