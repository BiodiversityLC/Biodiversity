using UnityEngine;

namespace Biodiversity.Creatures.Core.Search;

public abstract class SearchStrategy<TBlackboard, TAdapter>(AIContext<TBlackboard, TAdapter> ctx)
    where TBlackboard : IEnemyBlackboard
    where TAdapter : IEnemyAdapter
{
    public abstract void Start();
    public abstract bool TryGetNextSearchPosition(out Vector3 nextPosition);
    public abstract void Update();
    public abstract void Conclude();
    
    protected readonly AIContext<TBlackboard, TAdapter> context = ctx;
}