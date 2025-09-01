using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Util;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.SearchStrategies;

public class DistanceScorer(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx, float maxSearchRadius)
    : UtilityScorer<WaxSoldierBlackboard, WaxSoldierAdapter>(ctx)
{
    public override float Score(Vector3 candidatePosition, object visualizerOwner)
    {
        float distance = Vector3.Distance(candidatePosition, context.Blackboard.LastKnownPlayerPosition);
        float normalizedDistance = Mathf.Clamp01(distance / maxSearchRadius);
        float invertedDistance = 1.0f - normalizedDistance; // We want smaller distances to have a higher score
        return ExtensionMethods.Quadratic(invertedDistance, 0.5f);
    }
}