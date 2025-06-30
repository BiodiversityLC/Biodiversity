using UnityEngine;

namespace Biodiversity.Creatures.Core.Search;

public abstract class UtilityScorer<TBlackboard, TAdapter>(AIContext<TBlackboard, TAdapter> ctx)
    where TBlackboard : IEnemyBlackboard
    where TAdapter : IEnemyAdapter
{
    protected readonly AIContext<TBlackboard, TAdapter> context = ctx;

    public abstract float Score(Vector3 candidatePosition);
}