using Biodiversity.Util.Attributes;
using Biodiversity.Util.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Biodiversity.Creatures;

/// <summary>
/// An abstract base class for AI components that manage and transition between different behavior states.
/// This class provides a state management system where each state is represented by a <see cref="BehaviourState{TState, TEnemyAI}"/> object,
/// and transitions between states are managed by a dictionary of state types to state instances.
///
/// This class makes the creation of an AI a bit more complicated initially; however, it makes debugging and management of the AI significantly easier.
/// </summary>
/// <typeparam name="TState">An enumeration that represents the various states that the AI can be in.</typeparam>
/// <typeparam name="TEnemyAI">The specific AI class that inherits from <see cref="StateManagedAI{TState, TEnemyAI}"/>.</typeparam>
public abstract class StateManagedAI<TState, TEnemyAI> : BiodiverseAI
    where TState : Enum
    where TEnemyAI : StateManagedAI<TState, TEnemyAI>
{
    /// <summary>
    /// The current active state of the AI.
    /// </summary>
    protected BehaviourState<TState, TEnemyAI> CurrentState;
    
    /// <summary>
    /// The previous state of the AI before the current state.
    /// </summary>
    protected internal BehaviourState<TState, TEnemyAI> PreviousState;

    /// <summary>
    /// A dictionary mapping each <typeparamref name="TState"/> to its corresponding <see cref="BehaviourState{TState, TEnemyAI}"/> instance.
    /// This dictionary is populated in <see cref="ConstructStateDictionary"/> by reflecting over all types derived from <see cref="BehaviourState{TState, TEnemyAI}"/>.
    /// </summary>
    private readonly Dictionary<TState, BehaviourState<TState, TEnemyAI>> _stateDictionary = new();

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        ConstructStateDictionary();
        SwitchBehaviourState(DetermineInitialState());
    }

    /// <summary>
    /// Executes <see cref="BehaviourState{TState, TEnemyAI}.UpdateBehaviour"/> on the current state if <see cref="ShouldRunUpdate"/> returns true.
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (!ShouldRunUpdate()) return;
        
        CurrentState?.UpdateBehaviour();
    }

    /// <summary>
    /// Invoked at AI-defined intervals <see cref="EnemyAI.AIIntervalTime"/>.
    /// Executes the <see cref="BehaviourState{TState, TEnemyAI}.AIIntervalBehaviour"/> for the current state.
    /// Also checks for state transitions by evaluating each transition in the current state's transition list.
    /// </summary>
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!ShouldRunAiInterval()) return;
        
        CurrentState?.AIIntervalBehaviour();

        foreach (StateTransition<TState, TEnemyAI> transition in (CurrentState?.Transitions ?? []).Where(transition => transition.ShouldTransitionBeTaken()))
        {
            transition.OnTransition();
            LogVerbose("SwitchBehaviourState() called from DoAIInterval()");
            SwitchBehaviourState(transition.NextState());
            break;
        }
    }

    /// <summary>
    /// Executes <see cref="BehaviourState{TState, TEnemyAI}.LateUpdateBehaviour"/> on the current state if <see cref="ShouldRunLateUpdate"/> returns true.
    /// </summary>
    protected virtual void LateUpdate()
    {
        if (!ShouldRunLateUpdate()) return;
        CurrentState?.LateUpdateBehaviour();
    }
    
    /// <summary>
    /// Constructs the state dictionary by discovering all non-abstract subclasses of 
    /// <see cref="BehaviourState&lt;TState, TEnemyAI&gt;"/> that are decorated with the <see cref="StateAttribute"/> attribute.
    /// This method uses reflection to locate these subclasses, retrieve their associated <typeparamref name="TState"/> values,
    /// and instantiate them using their constructors.
    /// </summary>
    /// <remarks>
    /// This method relies on the <see cref="StateAttribute"/> to associate each subclass of 
    /// <see cref="BehaviourState&lt;TState, TEnemyAI&gt;"/> with a specific <typeparamref name="TState"/> value.
    /// Classes without the <see cref="StateAttribute"/> are skipped during the discovery process.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a state subclass is missing a required constructor or if instantiation fails.
    /// </exception>
    /// <example>
    /// Example of a valid subclass:
    /// <code>
    /// [State(MyStateEnum.SomeState)]
    /// public class SomeState : BehaviourState&lt;MyStateEnum, MyEnemyAI&gt;
    /// {
    ///     public SomeState(MyEnemyAI enemyAiInstance) : base(enemyAiInstance)
    ///     {
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="StateAttribute"/>
    private void ConstructStateDictionary()
    {
        if (!IsServer) return;
        
        // todo: cache stuff in this function because reflection is bad. Make sure to use a separate class because static thingies in generic classes (in this case) is bad.
        List<Type> stateTypes = BiodiversityPlugin.CachedAssemblies.Value
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(BehaviourState<TState, TEnemyAI>)) && !t.IsAbstract)
            .ToList();

        foreach (Type stateType in stateTypes)
        {

            StateAttribute attribute = stateType.GetCustomAttribute<StateAttribute>();
            if (attribute == null)
            {
                LogVerbose($"State type {stateType.FullName} is missing a StateAttribute");
                continue;
            }

            TState stateValue;
            try
            {
                stateValue = (TState)attribute.StateType;
            }
            catch (InvalidCastException)
            {
                LogError($"StateAttribute on {stateType.Name} does not match expected type {typeof(TState).Name}.");
                continue;
            }
            
            // Log constructors for debugging
            // ConstructorInfo[] constructors = stateType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // foreach (ConstructorInfo ctor in constructors)
            // {
            //     LogVerbose($"Constructor found for {stateType.Name}: {ctor}");
            // }
            
            ConstructorInfo constructor = stateType.GetConstructor([typeof(TEnemyAI)]);
            if (constructor == null)
            {
                LogError($"No valid matching constructor found for {stateType.Name}.");
                continue;
            }

            try
            {
                // Create an instance of this state by invoking the constructor
                BehaviourState<TState, TEnemyAI> stateInstance = (BehaviourState<TState, TEnemyAI>)constructor.Invoke([(TEnemyAI)this]);

                // Add the state to the dictionary if not already present
                if (!_stateDictionary.TryAdd(stateValue, stateInstance)) 
                    LogError($"State {stateValue} already exists in the dictionary.");
                else
                    LogVerbose($"State {stateValue} was added successfully.");
            }
            catch (Exception ex)
            {
                LogError($"Failed to instantiate {stateType.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Switches the current state of the AI to a new state.
    /// If the transition is valid, the current state's exit logic is called, followed by the entry logic of the new state.
    /// </summary>
    /// <param name="newState">The new state to switch to, represented by a <typeparamref name="TState"/> value.</param>
    /// <param name="stateTransition">The transition triggering the state change.</param>
    /// <param name="initData">Optional initialization data passed to the new state on entry.</param>
    internal void SwitchBehaviourState(
        TState newState,
        StateTransition<TState, TEnemyAI> stateTransition = null,
        StateData initData = null)
    {
        if (!IsServer) return;
        if (CurrentState != null)
        {
            LogVerbose($"Exiting state {CurrentState.GetStateType()}.");
            
            CurrentState.OnStateExit();
            PreviousState = CurrentState;
            previousBehaviourStateIndex = currentBehaviourStateIndex;
            
            stateTransition?.OnTransition();
        }
        else
        {
            LogVerbose("Could not exit the current state; it is null.");
        }

        if (_stateDictionary.TryGetValue(newState, out BehaviourState<TState, TEnemyAI> newStateInstance))
        {
            CurrentState = newStateInstance;
            currentBehaviourStateIndex = Convert.ToInt32(newState);
            LogVerbose($"Entering state {newState}.");
            
            CurrentState.OnStateEnter(ref initData);
            
            // todo: add network variable for current behaviour state index
            
            LogVerbose($"Successfully switched to behaviour state {newState}");
        }
        else
        {
            LogError($"State {newState} was not found in the StateDictionary. This should not happen.");
        }
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
}