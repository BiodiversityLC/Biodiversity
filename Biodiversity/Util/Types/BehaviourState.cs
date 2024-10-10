using Biodiversity.Creatures;
using System;
using System.Collections.Generic;

namespace Biodiversity.Util.Types;

/// <summary>
/// Represents a base state for managing AI behaviors in a state machine.
/// This is an abstract class that can be inherited to define specific states for an AI.
/// </summary>
/// <typeparam name="TState">The type of the state, typically an enum representing different AI states.</typeparam>
/// <typeparam name="TEnemyAI">The type of the AI that manages the states, typically a subclass of <see cref="StateManagedAI{TState, TEnemyAI}"/>.</typeparam>
public abstract class BehaviourState<TState, TEnemyAI>
    where TState : Enum
    where TEnemyAI : StateManagedAI<TState, TEnemyAI>
{
    /// <summary>
    /// The AI instance that is managed by this state.
    /// This is used to interact with the AI and update its behavior based on the current state.
    /// </summary>
    protected readonly TEnemyAI EnemyAIInstance;
    
    /// <summary>
    /// The current state type, typically an enum value representing a specific state.
    /// </summary>
    private readonly TState _stateType;

    /// <summary>
    /// A list of transitions from this state to other states.
    /// These transitions define the conditions under which the AI should change to a different state.
    /// </summary>
    protected internal List<StateTransition<TState, TEnemyAI>> Transitions { get; protected set; } = [];
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BehaviourState{TState, TEnemyAI}"/> class.
    /// </summary>
    /// <param name="enemyAiInstance">The AI instance associated with this state.</param>
    /// <param name="stateType">The specific state type represented by this instance, usually an enum.</param>
    protected BehaviourState(TEnemyAI enemyAiInstance, TState stateType)
    {
        EnemyAIInstance = enemyAiInstance ?? throw new ArgumentNullException(nameof(enemyAiInstance));
        _stateType = stateType;
    }

    /// <summary>
    /// Called when the AI enters this state.
    /// Override this method to define behavior that should happen when the AI transitions to this state.
    /// </summary>
    /// <param name="initData">The initialization data that can be passed to the state when it is entered.</param>
    internal virtual void OnStateEnter(ref StateData initData)
    {
        EnemyAIInstance.LogEnemyError($"OnStateEnter called for {_stateType}.");
        initData ??= new StateData();
    }

    /// <summary>
    /// Performs behavior in the AI's Update cycle while it is in this state.
    /// Override this method to define continuous behavior for the AI.
    /// </summary>
    internal virtual void UpdateBehaviour()
    {
    }
    
    /// <summary>
    /// Performs behavior in the AI's DoAIInterval cycle while it is in this state.
    /// This method can be overridden to define logic that should be executed at regular intervals.
    /// </summary>
    internal virtual void AIIntervalBehaviour()
    {
    }

    /// <summary>
    /// Performs behavior in the AI's LateUpdate cycle while it is in this state.
    /// Override this method to define behavior that should happen at the end of the frame.
    /// </summary>
    internal virtual void LateUpdateBehaviour()
    {
    }

    /// <summary>
    /// Called when the AI exits this state.
    /// Override this method to define behavior that should happen when the AI transitions out of this state.
    /// </summary>
    internal virtual void OnStateExit()
    {
        EnemyAIInstance.LogVerbose($"OnStateExit called for {_stateType}.");
    }

    /// <summary>
    /// Gets the current state type.
    /// </summary>
    /// <returns>The state type represented by this state instance, an enum value.</returns>
    internal TState GetStateType()
    {
        return _stateType;
    }
}