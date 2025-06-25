using UnityEngine;

namespace Biodiversity.Creatures.Core.Search;

public interface ISearchStrategy<TBlackboard, TAdapter>
    where TBlackboard : IEnemyBlackboard
    where TAdapter : IEnemyAdapter
{
    /// <summary>
    /// The name of the strategy for debugging and configuration.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Initializes the search strategy with the necessary AI context for its operation.
    /// </summary>
    /// <param name="ctx">The shared context object providing access to the AI's adapter and blackboard.</param>
    /// <remarks>
    /// This method serves as the entry point and dependency injection mechanism for the strategy.
    /// It is called exactly once when the AI enters a state that uses this strategy.
    /// Implementations should cache the provided <paramref name="ctx"/> instance for use in subsequent
    /// update or execution calls. This is also the ideal place to perform any one-time setup
    /// calculations, like identifying an initial search area based on the blackboard's state.
    /// </remarks>
    void Initialize(AIContext<TBlackboard, TAdapter> ctx);

    bool TryGetNextSearchPosition(out Vector3 nextPosition);

    void Update();
    void Conclude();
}