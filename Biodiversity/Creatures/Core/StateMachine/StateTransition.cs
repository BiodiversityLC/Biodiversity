using System;

namespace Biodiversity.Creatures.Core.StateMachine;

/// <summary>
/// Represents a transition between two states in an AI's state machine.
/// This abstract class defines the structure for evaluating whether a transition should occur and what the next state should be.
/// </summary>
/// <typeparam name="TState">The type of the state, typically an enum representing different AI states.</typeparam>
/// <typeparam name="TEnemyAI">The type of the AI that manages the states, typically a subclass of <see cref="StateManagedAI{TState,TEnemyAI}"/>.</typeparam>
public abstract class StateTransition<TState, TEnemyAI>
    where TState : Enum
    where TEnemyAI : StateManagedAI<TState, TEnemyAI>
{
    /// <summary>
    /// The AI instance that is managed by this transition.
    /// This is used to evaluate the transition logic and move the AI to the next state.
    /// </summary>
    protected readonly TEnemyAI EnemyAIInstance;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="StateTransition{TState, TEnemyAI}"/> class.
    /// </summary>
    /// <param name="enemyAIInstance">The AI instance associated with this transition.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enemyAIInstance"/> is <c>null</c>.</exception>
    protected StateTransition(TEnemyAI enemyAIInstance)
    {
        EnemyAIInstance = enemyAIInstance ?? throw new ArgumentNullException(nameof(enemyAIInstance));
    }

    /// <summary>
    /// Determines whether the transition should be taken based on the current state and conditions in the AI.
    /// This method must be overridden in a subclass to define the specific conditions under which the AI should transition to another state.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the transition should be taken; otherwise, <c>false</c>.
    /// </returns>
    internal abstract bool ShouldTransitionBeTaken();
    
    /// <summary>
    /// Gets the next state that the AI should transition to.
    /// This method must be overridden in a subclass to specify the next state that should be activated after the transition.
    /// </summary>
    /// <returns>The next state of type <typeparamref name="TState"/>.</returns>
    internal abstract TState NextState();
    
    /// <summary>
    /// Called when the transition is performed.
    /// Override this method to define behavior that should occur when the transition is triggered.
    /// </summary>
    internal virtual void OnTransition()
    {
    }

    /// <summary>
    /// Gets a description of the transition.
    /// Override this method to provide details or context for the transition, such as for debugging or logging purposes.
    /// </summary>
    /// <returns>A string that describes the transition. The default implementation returns an empty string.</returns>
    internal virtual string GetTransitionDescription()
    {
        return "";
    }
}