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
    /// Invoked at AI-defined intervals (defined in the Unity inspector).
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
    /// Constructs the state dictionary by discovering all non-abstract subclasses of <see cref="BehaviourState{TState, TEnemyAI}"/>
    /// and adding them to the dictionary with their respective <typeparamref name="TState"/> keys.
    /// This method uses reflection to find appropriate types and constructors.
    /// </summary>
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
            // Extract the specific TState type from the subclass
            Type[] genericArgs = stateType.BaseType?.GetGenericArguments();
            if (genericArgs is not { Length: 2 } || genericArgs[1] != typeof(TEnemyAI))
            {
                LogError($"Type {stateType.Name} does not match expected BehaviourState<TState, TEnemyAI> signature.");
                continue;
            }

            // The first generic argument is the TState enum
            Type stateEnumType = genericArgs[0];
            if (!stateEnumType.IsEnum)
            {
                LogError($"The generic type {stateEnumType.Name} is not an enum.");
                continue;
            }
            
            FieldInfo[] fields = stateType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            LogVerbose($"Inspecting {fields.Length} fields in {stateType.Name}.");
            
            // Find a public/internal static field matching the TState enum
            FieldInfo stateField = fields.FirstOrDefault(f =>
                f.FieldType == stateEnumType && (f.IsPublic || f.IsAssembly));

            if (stateField == null)
            {
                LogError($"No public/internal static field found in {stateType.Name} that matches the {stateEnumType.Name} enum.");
                continue;
            }
            
            LogVerbose($"Found matching field: {stateField.Name} in {stateType.Name}.");

            // Get the TState enum value from the static field
            TState stateValue;
            try
            {
                stateValue = (TState)stateField.GetValue(null);
            }
            catch (Exception ex)
            {
                LogError($"Failed to retrieve value from field {stateField.Name}: {ex.Message}");
                continue;
            }
            
            // Get the constructor that matches the (TEnemyAI) signature
            ConstructorInfo constructor = stateType.GetConstructor([typeof(TEnemyAI)]);
            if (constructor == null)
            {
                LogError($"No matching constructor found for {stateType.Name}.");
                continue;
            }

            try
            {
                // Create an instance of this state by invoking the constructor
                BehaviourState<TState, TEnemyAI> stateInstance = (BehaviourState<TState, TEnemyAI>)constructor.Invoke([(TEnemyAI)this]);

                // Add the state to the dictionary if not already present
                if (!_stateDictionary.TryAdd(stateValue, stateInstance))
                    LogError($"State {stateValue} already exists in the dictionary.");
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