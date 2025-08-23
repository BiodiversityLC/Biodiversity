using Biodiversity.Core.Attributes;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Biodiversity.Creatures.Core.StateMachine;

/// <summary>
/// Represents a base state for managing AI behaviors in a state machine.
/// This is an abstract class that can be inherited to define specific states for an AI.
/// </summary>
/// <typeparam name="TState">The type of the state, typically an enum representing different AI states.</typeparam>
/// <typeparam name="TEnemyAI">The type of the AI that manages the states, typically a subclass of <see cref="StateManagedAI{TState,TEnemyAI}"/>.</typeparam>
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
    /// A cache for storing the mapping between derived state types and their corresponding state values.
    /// This improves performance by avoiding repeated reflection to retrieve the state value from the
    /// <see cref="StateAttribute"/> of each derived class.
    /// </summary>
    /// <remarks>
    /// The cache is keyed by the <see cref="Type"/> of the derived class and stores the associated
    /// <typeparamref name="TState"/> value specified in the <see cref="StateAttribute"/>.
    /// </remarks>
    private static readonly Dictionary<Type, TState> StateTypeCache = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BehaviourState{TState, TEnemyAI}"/> class.
    /// </summary>
    /// <param name="enemyAiInstance">The AI instance associated with this state.</param>
    protected BehaviourState(TEnemyAI enemyAiInstance)
    {
        EnemyAIInstance = enemyAiInstance ?? throw new ArgumentNullException(nameof(enemyAiInstance));
        _stateType = GetStateTypeFromAttribute();
    }

    /// <summary>
    /// Retrieves the state type associated with the derived class by inspecting its <see cref="StateAttribute"/>.
    /// If the state type is already cached, it is returned directly. Otherwise, it is retrieved via reflection,
    /// cached for future use, and then returned.
    /// </summary>
    /// <returns>
    /// The <typeparamref name="TState"/> value specified in the <see cref="StateAttribute"/> of the derived class.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the derived class does not have a valid <see cref="StateAttribute"/> or if the
    /// attribute's state type does not match <typeparamref name="TState"/>.
    /// </exception>
    /// <remarks>
    /// This method ensures that the state type is efficiently retrieved and cached to minimize the
    /// performance overhead of repeated reflection.
    /// </remarks>
    private TState GetStateTypeFromAttribute()
    {
        Type derivedType = GetType();
        if (StateTypeCache.TryGetValue(derivedType, out TState cachedState))
            return cachedState;
        
        StateAttribute attribute = derivedType.GetCustomAttribute<StateAttribute>();
        if (attribute is not { StateType: TState state })
            throw new InvalidOperationException($"Class {derivedType.Name} must have a valid StateAttribute.");
        
        StateTypeCache[derivedType] = state;
        return state;
    }

    /// <summary>
    /// Called when the AI enters this state.
    /// Override this method to define behavior that should happen when the AI transitions to this state.
    /// </summary>
    /// <param name="initData">The initialization data that can be passed to the state when it is entered.</param>
    internal virtual void OnStateEnter(ref StateData initData)
    {
        EnemyAIInstance.LogVerbose($"OnStateEnter called for {_stateType}.");
        initData ??= new StateData();
    }
    
    /// <summary>
    /// Called when the AI exits this state.
    /// Override this method to define behavior that should happen when the AI transitions out of this state.
    /// </summary>
    /// <param name="transition">The transition that is causing the state to exit. Can be null if the state change was forced.</param>
    /// <remarks>
    /// Common usages of this method are to clean up any resources, stop coroutines, or reset variables.
    /// </remarks>
    internal virtual void OnStateExit(StateTransition<TState, TEnemyAI> transition)
    {
        EnemyAIInstance.LogVerbose($"{nameof(OnStateExit)} called for {_stateType}.");
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
    /// Performs behavior in the AI's FixedUpate cycle while it is in this state.
    /// </summary>
    internal virtual void FixedUpdateBehaviour()
    {
    }
    
    /// <summary>
    /// Called when <see cref="TEnemyAI"/> is stunned. This lets the current behaviour state react, and optionally supress
    /// the <see cref="TEnemyAI"/>'s default stun reaction.
    /// </summary>
    /// <param name="setToStunned">If <c>true</c>, then make the AI stunned.</param>
    /// <param name="setToStunTime">How long the stun lasts for in seconds.</param>
    /// <param name="setStunnedByPlayer">The player that caused the stun, if any; otherwise <c>null</c></param>
    /// <returns>
    /// <c>true</c> if this state fully handled the event and the default reaction should NOT run;
    /// otherwise <c>false</c> to allow the <see cref="TEnemyAI"/>'s default handling.
    /// </returns>
    /// <remarks>
    /// Override in states that need custom behavior. Return <c>false</c> to run the shared default logic;
    /// return <c>true</c> to suppress it.
    /// </remarks>
    internal virtual bool OnSetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        EnemyAIInstance.LogVerbose($"{nameof(OnSetEnemyStunned)} called for {_stateType}.");
        return false;
    }

    /// <summary>
    /// Called when <see cref="TEnemyAI"/> is hit. This lets the current behaviour state react, and optionally supress
    /// the <see cref="TEnemyAI"/>'s default hit reaction.
    /// </summary>
    /// <param name="force">The amount of damage that was done by the hit.</param>
    /// <param name="playerWhoHit">The player that caused the hit, if any; otherwise <c>null</c></param>
    /// <param name="hitId">The ID of the hit.</param>
    /// <returns>
    /// <c>true</c> if this state fully handled the event and the default reaction should NOT run;
    /// otherwise <c>false</c> to allow the <see cref="TEnemyAI"/>'s default handling.
    /// </returns>
    /// <remarks>
    /// Override in states that need custom behavior. Return <c>false</c> to run the shared default logic;
    /// return <c>true</c> to suppress it.
    /// </remarks>
    internal virtual bool OnHitEnemy(
        int force = 1, 
        PlayerControllerB playerWhoHit = null, 
        int hitId = -1)
    {
        EnemyAIInstance.LogVerbose($"{nameof(OnHitEnemy)} called for {_stateType}.");
        return false;
    }

    /// <summary>
    /// Called to handle any AI-specific custom events that don't have a dedicated virtual method.
    /// Uses a switch on the <see cref="eventName"/> to handle different custom events.
    /// </summary>
    /// <param name="eventName">The unique string identifier for the custom event.</param>
    /// <param name="eventData">An optional payload of data for the event.</param>
    internal virtual void OnCustomEvent(string eventName, StateData eventData)
    {
        EnemyAIInstance.LogVerbose($"{nameof(OnCustomEvent)} with name {eventName} called for {_stateType}.");
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