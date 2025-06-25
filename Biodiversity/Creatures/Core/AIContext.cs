namespace Biodiversity.Creatures.Core;

/// <summary>
/// A container for all services and data sources that AI behaviors need to operate.
/// This simplifies dependency management (e.g. when using the strategy pattern to make search algorithms)
/// by passing a single context object instead of multiple parameters.
/// </summary>
/// <typeparam name="TBlackboard">The concrete enemy blackboard.</typeparam>
/// <typeparam name="TAdapter">The concrete enemy adapter.</typeparam>
public class AIContext<TBlackboard, TAdapter>(TBlackboard blackboard, TAdapter adapter)
    where TBlackboard : IEnemyBlackboard
    where TAdapter : IEnemyAdapter
{
    public TBlackboard Blackboard { get; } = blackboard;
    public TAdapter Adapter { get; } = adapter;
}