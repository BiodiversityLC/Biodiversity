﻿using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Core.StateMachine;

/// <summary>
/// An abstract base class for AI components that manage and transition between different behavior states.
/// This class provides a robust, reflection-based state machine system where each state is represented by a
/// <see cref="BehaviourState{TState,TEnemyAI}"/> object. Transitions between states are managed based on
/// defined conditions within each state.
/// </summary>
/// <remarks>
/// <para>
/// This state machine pattern enhances modularity and simplifies the management of complex AI behaviors.
/// States are discovered at runtime using reflection by looking for classes derived from
/// <see cref="BehaviourState{TState,TEnemyAI}"/> and decorated with the <see cref="StateAttribute"/>.
/// </para>
/// <para>
/// To optimize performance, discovered state types and their constructors are cached statically per
/// unique combination of <see cref="TState"/> and <see cref="TEnemyAI"/>.
/// This means reflection overhead is incurred only once when the first AI of a specific type initializes.
/// </para>
/// <para>
/// Networking: The current state index is synchronized using a <see cref="NetworkVariable{T}"/>,
/// allowing client-side logic (e.g., animations, effects) to react to server-driven state changes.
/// All core state logic and transitions execute exclusively on the server.
/// </para>
/// </remarks>
/// <typeparam name="TState">An enumeration (Enum) that defines the possible states for this AI.</typeparam>
/// <typeparam name="TEnemyAI">The concrete AI class that inherits from this <see cref="StateManagedAI{TState, TEnemyAI}"/>.</typeparam>
public abstract class StateManagedAI<TState, TEnemyAI> : BiodiverseAI
    where TState : Enum
    where TEnemyAI : StateManagedAI<TState, TEnemyAI>
{
    /// <summary>
    /// Provides a static cache for state types and their constructors, specific to each
    /// combination of the class's generic type parameters <see cref="TState"/> and <see cref="TEnemyAI"/>.
    /// This significantly reduces reflection overhead after the initial setup.
    /// </summary>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType",
        Justification = "The static members in StateCache are intentionally specific to the generic type parameters TState and TEnemyAI. " +
                        "This ensures that each unique combination of AI state enum and AI enemy type gets its own distinct cache " +
                        "for its BehaviourState types and constructors. This is the desired behavior for type-safe, " +
                        "performant state discovery and instantiation per AI specialization.")]
    private static class StateCache
    {
        /// <summary>
        /// Gets a dictionary mapping state enum values (of type <see cref="TState"/>) to their corresponding <see cref="Type"/>
        /// of the <see cref="BehaviourState{TState,TEnemyAI}"/> implementation.
        /// Populated during the first initialization for this specific <see cref="TState"/> and <see cref="TEnemyAI"/> combination.
        /// </summary>
        public static Dictionary<TState, Type> StateTypes { get; private set; }
        
        /// <summary>
        /// Gets a dictionary mapping <see cref="Type"/> of the <see cref="BehaviourState{TState,TEnemyAI}"/> implementation
        /// to its <see cref="ConstructorInfo"/>. This constructor is expected to take a single parameter of type <see cref="TEnemyAI"/>.
        /// Populated during the first initialization for this specific <see cref="TState"/> and <see cref="TEnemyAI"/> combination.
        /// </summary>
        public static Dictionary<Type, ConstructorInfo> StateConstructors { get; private set; }
        
        /// <summary>
        /// Gets a value indicating whether this specific <see cref="StateCache"/> (for this <see cref="TState"/>, <see cref="TEnemyAI"/> pair)
        /// has been initialized.
        /// </summary>
        private static bool IsInitialized { get; set; }

        /// <summary>
        /// Initializes the state cache if it hasn't been already for this specific combination of <see cref="TState"/> and <see cref="TEnemyAI"/>.
        /// This method uses reflection to find all relevant <see cref="BehaviourState{TState,TEnemyAI}"/> implementations
        /// within specified assemblies, validates them, and stores their types and constructors.
        /// This method is thread-safe using a lock for the initialization block.
        /// </summary>
        public static void InitializeIfNeeded()
        {
            if (IsInitialized) return;
            lock (typeof(StateCache)) // Ensures thread safety during initialization
            {
                if (IsInitialized) return; // Double check lock
                
                StateTypes = new Dictionary<TState, Type>();
                StateConstructors = new Dictionary<Type, ConstructorInfo>();

                Type stateBehaviourBaseType = typeof(BehaviourState<TState, TEnemyAI>);
                Type enemyAiType = typeof(TEnemyAI);

                for (int i = 0; i < BiodiversityPlugin.CachedAssemblies.Value.Count; i++)
                {
                    Assembly assembly = BiodiversityPlugin.CachedAssemblies.Value[i];
                    if (!assembly.FullName.Contains("Biodiversity")) continue;

                    try
                    {
                        foreach (Type type in assembly.GetLoadableTypes())
                        {
                            if (!type.IsAbstract && type.IsSubclassOf(stateBehaviourBaseType))
                            {
                                StateAttribute attribute = type.GetCustomAttribute<StateAttribute>();
                                if (attribute == null)
                                {
                                    BiodiversityPlugin.Logger.LogError(
                                        $"[StateCache<{typeof(TEnemyAI).Name}>] State type {type.FullName} is missing a StateAttribute");
                                    continue;
                                }

                                TState stateValue;
                                try
                                {
                                    stateValue = (TState)attribute.StateType;
                                }
                                catch (InvalidCastException)
                                {
                                    BiodiversityPlugin.Logger.LogError(
                                        $"[StateCache<{typeof(TEnemyAI).Name}>] StateAttribute on {type.Name} does not match expected enum type {typeof(TState).Name}.");
                                    continue;
                                }

                                ConstructorInfo constructor = type.GetConstructor([enemyAiType]);
                                if (constructor == null)
                                {
                                    BiodiversityPlugin.Logger.LogError(
                                        $"[StateCache<{typeof(TEnemyAI).Name}>] No constructor matching ({enemyAiType.Name}) found for state type {type.Name}.");
                                    continue;
                                }

                                if (!StateTypes.TryAdd(stateValue, type))
                                {
                                    BiodiversityPlugin.Logger.LogError(
                                        $"[StateCache<{typeof(TEnemyAI).Name}>] Duplicate state enum value {stateValue} detected for type {type.Name} (Already mapped to {StateTypes[stateValue].Name}).");
                                    continue;
                                }

                                // Cache the constructor along with the type
                                StateConstructors.Add(type, constructor);

                                BiodiversityPlugin.LogVerbose(
                                    $"[StateCache<{typeof(TEnemyAI).Name}>] Cached state {stateValue} -> {type.Name}");
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        BiodiversityPlugin.Logger.LogError(
                            $"[StateCache<{typeof(TEnemyAI).Name}>] Failed to load types from assembly {assembly.FullName}: {e.Message}");
                    }
                    catch (Exception e)
                    {
                        BiodiversityPlugin.Logger.LogError($"[StateCache<{typeof(TEnemyAI).Name}>] Error processing assembly {assembly.FullName}: {e}");
                    }
                }
                
                BiodiversityPlugin.Logger.LogInfo($"[StateCache<{typeof(TEnemyAI).Name}>] Initialized with {StateTypes.Count} states.");
                IsInitialized = true;
            }
        }
    }
    
    /// <summary>
    /// Gets the current active behavior state instance of the AI. This state is an implementation of <see cref="BehaviourState{TState,TEnemyAI}"/>.
    /// This is <c>null</c> if no state is active or before initialization.
    /// </summary>
    protected BehaviourState<TState, TEnemyAI> CurrentState;
    
    /// <summary>
    /// Network-synchronized variable holding the integer representation of the current AI state (which is of enum type <see cref="TState"/>).
    /// Primarily used for synchronizing state information to clients for visual or non-gameplay logic.
    /// Marked as public for (and only for) vanilla compatibility or external systems needing to read the raw state index,
    /// though direct manipulation from outside this class is discouraged.
    /// </summary>
    public readonly NetworkVariable<int> NetworkCurrentBehaviourStateIndex = new();
    
    /// <summary>
    /// Gets the behavior state instance (an implementation of <see cref="BehaviourState{TState,TEnemyAI}"/>)
    /// that was active before the <see cref="CurrentState"/>.
    /// This is <c>null</c> if the AI has not transitioned from a previous state yet.
    /// </summary>
    protected internal BehaviourState<TState, TEnemyAI> PreviousState;

    /// <summary>
    /// A dictionary mapping each <see cref="TState"/> enum value to its instantiated
    /// <see cref="BehaviourState{TState,TEnemyAI}"/> object for this specific AI instance.
    /// This dictionary is populated by <see cref="ConstructStateDictionary"/> during this AI's <see cref="Start"/> method.
    /// </summary>
    private readonly Dictionary<TState, BehaviourState<TState, TEnemyAI>> _stateDictionary = new();

    /// <summary>
    /// A dictionary containing arrays of <see cref="AudioClip"/>s, indexed by category name.
    /// </summary>
    protected Dictionary<string, AudioClip[]> AudioClips { get; set; } = new();
    
    /// <summary>
    /// A dictionary containing <see cref="AudioSource"/> components, indexed by category name.
    /// </summary>
    protected Dictionary<string, AudioSource> AudioSources { get; set; } = new();
    
    /// <summary>
    /// Called when the script instance is being loaded.
    /// Initializes the base <see cref="BiodiverseAI"/> and, if on the server,
    /// constructs the state dictionary and transitions to the initial state determined by <see cref="DetermineInitialState"/>.
    /// </summary>
    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        ConstructStateDictionary();
        
        if (_stateDictionary.Count > 0) SwitchBehaviourState(DetermineInitialState());
        else LogError("State dictionary is empty after construction. The AI will not work at all.");
    }

    /// <summary>
    /// Called every frame, if the MonoBehaviour is enabled.
    /// Executes the <see cref="BehaviourState{TState,TEnemyAI}.UpdateBehaviour"/> method of the <see cref="CurrentState"/>
    /// if <see cref="ShouldRunUpdate"/> returns <c>true</c>.
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (!ShouldRunUpdate()) return;
        
        CurrentState?.UpdateBehaviour();
    }

    /// <summary>
    /// Called at fixed intervals defined by <c>EnemyAI.AIIntervalTime</c> (from a base class, not shown).
    /// Executes the <see cref="BehaviourState{TState,TEnemyAI}.AIIntervalBehaviour"/> of the <see cref="CurrentState"/>
    /// and then checks for any valid state transitions defined in the current state.
    /// This is the primary driver for state changes.
    /// </summary>
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!ShouldRunAiInterval()) return;
        
        CurrentState?.AIIntervalBehaviour();
        
        List<StateTransition<TState, TEnemyAI>> transitions = CurrentState?.Transitions ?? [];
        for (int i = 0; i < transitions.Count; i++)
        {
            StateTransition<TState, TEnemyAI> transition = transitions[i];
            if (!transition.ShouldTransitionBeTaken()) continue;
            
            transition.OnTransition();
            LogVerbose("SwitchBehaviourState() called from DoAIInterval()");
            SwitchBehaviourState(transition.NextState());
            break;
        }
    }

    /// <summary>
    /// Called every frame after all <see cref="Update"/> functions have been called.
    /// Executes the <see cref="BehaviourState{TState,TEnemyAI}.LateUpdateBehaviour"/> method of the <see cref="CurrentState"/>
    /// if <see cref="ShouldRunLateUpdate"/> returns <c>true</c>.
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (!ShouldRunLateUpdate()) return;
        CurrentState?.LateUpdateBehaviour();
    }

    //todo: write xml doc for this
    protected void FixedUpdate()
    {
        if (!ShouldRunFixedUpdate()) return;
        CurrentState?.FixedUpdateBehaviour();
    }

    /// <summary>
    /// Populates the instance-specific <see cref="_stateDictionary"/> by creating instances of
    /// state types found in the <see cref="StateCache"/>. The state types are implementations of <see cref="BehaviourState{TState,TEnemyAI}"/>.
    /// This method is called once during <see cref="Start"/> for each AI instance.
    /// </summary>
    private void ConstructStateDictionary()
    {
        if (!IsServer) return;
        
        StateCache.InitializeIfNeeded(); // Ensure the static cache is initialized once per TState/TEnemyAI combo
        _stateDictionary.Clear();

        foreach ((TState stateValue, Type stateType) in StateCache.StateTypes)
        {
            if (!StateCache.StateConstructors.TryGetValue(stateType, out ConstructorInfo constructor))
            {
                // This should ideally not happen if InitializeIfNeeded worked correctly
                LogError($"Constructor cache miss for state type {stateType.Name}. Skipping state {stateValue}.");
                continue;
            }

            try
            {
                // Create an instance using the cached constructor
                BehaviourState<TState, TEnemyAI> stateInstance = (BehaviourState<TState, TEnemyAI>)constructor.Invoke([(TEnemyAI)this]);

                // Add the instance to this AI's dictionary
                _stateDictionary.Add(stateValue, stateInstance);

                LogVerbose($"Instantiated state {stateValue} ({stateType.Name}) for this AI instance.");
            }
            catch (Exception e)
            {
                LogError($"Failed to instantiate state {stateType.Name} using cached constructor: {e}");
            }
        }
        LogVerbose($"Constructed state dictionary for this instance with {_stateDictionary.Count} states.");
    }

    /// <summary>
    /// Transitions the AI to a new behavior state.
    /// This involves calling <see cref="BehaviourState{TState,TEnemyAI}.OnStateExit"/> on the current state (if any),
    /// then <see cref="BehaviourState{TState,TEnemyAI}.OnStateEnter"/> on the new state.
    /// The <see cref="NetworkCurrentBehaviourStateIndex"/> is updated to reflect the new state.
    /// The <paramref name="stateTransition"/>'s <c>OnTransition</c> method is called if provided.
    /// </summary>
    /// <param name="newState">The enum value of the <see cref="TState"/> to transition to.</param>
    /// <param name="stateTransition">The <see cref="StateTransition{TState,TEnemyAI}"/> object that triggered this state change, if applicable.
    /// Its <c>OnTransition</c> method will be called.</param>
    /// <param name="initData">Optional <see cref="StateData"/> to pass to the <c>OnStateEnter</c>
    /// method of the new state. If the state's <c>OnStateEnter</c> method signature uses <c>ref StateData</c>,
    /// then <paramref name="initData"/> will be passed by reference.
    /// </param>
    internal void SwitchBehaviourState(
        TState newState,
        StateTransition<TState, TEnemyAI> stateTransition = null,
        StateData initData = null)
    {
        if (!IsServer) return;
        
        BehaviourState<TState, TEnemyAI> previousStateInstance = CurrentState;
        if (previousStateInstance != null)
        {
            LogVerbose($"Exiting state {previousStateInstance.GetStateType()}.");

            try
            {
                previousStateInstance.OnStateExit();
            }
            catch (Exception e)
            {
                LogError($"Exception during OnStateExit for {previousStateInstance.GetStateType()}: {e}");
            }
            
            PreviousState = previousStateInstance;
            previousBehaviourStateIndex = Convert.ToInt32(previousStateInstance.GetStateType());
            
            stateTransition?.OnTransition();
        }
        else
        {
            LogVerbose("Could not exit the current state; it is null.");
            PreviousState = null;
            previousBehaviourStateIndex = -1;
        }

        if (_stateDictionary.TryGetValue(newState, out BehaviourState<TState, TEnemyAI> newStateInstance))
        {
            CurrentState = newStateInstance;
            currentBehaviourStateIndex = Convert.ToInt32(newState);
            ExtensionMethods.ChangeNetworkVar(NetworkCurrentBehaviourStateIndex, currentBehaviourStateIndex);
            
            LogVerbose($"Entering state {newState}.");

            try
            {
                CurrentState.OnStateEnter(ref initData);
            }
            catch (Exception e)
            {
                LogError($"Exception during OnStateEnter for {newState}: {e}");
            }
            
            LogVerbose($"Successfully switched to behaviour state {newState}");
        }
        else
        {
            LogError($"State {newState} was not found in the StateDictionary. This should not happen.");
        }
    }
    
    /// <summary>
    /// Triggers a custom, AI-specific event to be processed by the current state.
    /// </summary>
    /// <param name="eventName">A unique string identifying the event (e.g., "GrabAnimationComplete").</param>
    /// <param name="data">Optional data payload.</param>
    public void TriggerCustomEvent(string eventName, StateData data = null)
    {
        if (!IsServer) return;
        
        CurrentState?.OnCustomEvent(eventName, data);
    }

    /// <summary>
    /// Determines the initial state for the AI when it is initialized.
    /// This method should be overridden by subclasses to specify the starting state 
    /// for the AI's behavior state machine.
    /// </summary>
    /// <returns>
    /// The initial state of type <typeparamref name="TState"/> that the AI should enter at the start.
    /// </returns>
    /// <remarks>
    /// The implementation of this method can use various strategies to decide the initial state, 
    /// such as defaulting to a specific state, basing it on game context, or loading it from saved data.
    /// </remarks>
    /// <example>
    /// An example of an implementation:
    /// <code>
    /// protected override TState DetermineInitialState()
    /// {
    ///     return MyAIStates.Spawning;
    /// }
    /// </code>
    /// </example>
    protected abstract TState DetermineInitialState();

    /// <summary>
    /// Determines if the <see cref="Update"/> method should execute.
    /// This method is designed to be overridden by derived classes to add custom conditions for execution.
    /// By default, it returns true only if the object is on the server and the enemy is not dead.
    /// </summary>
    /// <returns><c>true</c> if <see cref="Update"/> should run; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldRunUpdate()
    {
        return IsServer && !isEnemyDead;
    }

    /// <summary>
    /// Determines if the <see cref="DoAIInterval"/> method should execute.
    /// This method is intended to be overridden by subclasses to add custom conditions for running the AI interval.
    /// By default, it returns true only if the object is on the server and the enemy is not dead.
    /// </summary>
    /// <returns><c>true</c> if <see cref="DoAIInterval"/> should run; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldRunAiInterval()
    {
        return IsServer && !isEnemyDead;
    }

    /// <summary>
    /// Determines if the <see cref="LateUpdate"/> method should execute.
    /// This method is intended to be overridden by subclasses to add custom conditions for executing late update logic.
    /// By default, it returns true only if the object is on the server and the enemy is not dead.
    /// </summary>
    /// <returns><c>true</c> if <see cref="LateUpdate"/> should run; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldRunLateUpdate()
    {
        return IsServer && !isEnemyDead;
    }

    /// <summary>
    /// Determines if the <see cref="FixedUpdate"/> method should execute.
    /// This method is intended to be overridden by subclasses to add custom conditions for executing fixed update logic.
    /// By default, it returns true only if the object is on the server and the enemy is not dead.
    /// </summary>
    /// <returns><c>true</c> if <see cref="FixedUpdate"/> should run; otherwise, <c>false</c>.</returns>
    protected virtual bool ShouldRunFixedUpdate()
    {
        return IsServer && !isEnemyDead;
    }
}