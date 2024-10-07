using Biodiversity.Util.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Biodiversity.Creatures;

internal abstract class StateManagedAI<TState, TEnemyAI> : BiodiverseAI
    where TState : Enum
    where TEnemyAI : StateManagedAI<TState, TEnemyAI>
{
    protected BehaviourState<TState, TEnemyAI> CurrentState;
    protected internal BehaviourState<TState, TEnemyAI> PreviousState;

    private readonly Dictionary<TState, BehaviourState<TState, TEnemyAI>> _stateDictionary = new();
    
    // todo: fix these warnings
    private static PerKeyCachedDictionary<Type, ConstructorInfo> _constructorCache;
    private static CachedList<Type> _stateTypeCache;

    protected override void Awake()
    {
        base.Awake();
        
        _stateTypeCache = new CachedList<Type>(() =>
        {
            IEnumerable<Type> stateTypes = BiodiversityPlugin.cachedAssemblies.Value
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(BehaviourState<TState, TEnemyAI>)) && !t.IsAbstract);

            return stateTypes.ToList();
        });

        _constructorCache = new PerKeyCachedDictionary<Type, ConstructorInfo>(stateType =>
            stateType.GetConstructor([typeof(TEnemyAI)]));
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        ConstructStateDictionary();
    }

    private void ConstructStateDictionary()
    {
        if (!IsServer) return;

        foreach (Type stateType in _stateTypeCache.Value)
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
            
            // Find a public static field matching the TState enum
            FieldInfo stateField = stateType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(f => f.FieldType == stateEnumType);

            if (stateField == null)
            {
                LogError($"No public static field found in {stateType.Name} that matches the {stateEnumType.Name} enum.");
                continue;
            }

            // Get the TState enum value from the static field
            TState stateValue = (TState)stateField.GetValue(null);
            
            // Get the constructor that matches the (TEnemyAI) signature
            ConstructorInfo constructor = _constructorCache[stateType];
            if (constructor == null)
            {
                LogError($"No matching constructor found for {stateType.Name}.");
                continue;
            }

            try
            {
                // Create an instance of this state by invoking the constructor
                BehaviourState<TState, TEnemyAI> stateInstance = (BehaviourState<TState, TEnemyAI>)constructor.Invoke(new object[] { (TEnemyAI)this });

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

    public override void Update()
    {
        base.Update();
        if (!ShouldRunUpdate()) return;
        
        CurrentState?.UpdateBehaviour();
    }

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

    private void LateUpdate()
    {
        if (!ShouldRunLateUpdate()) return;
        CurrentState?.LateUpdateBehaviour();
    }

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

    protected virtual bool ShouldRunUpdate()
    {
        return IsServer && !isEnemyDead;
    }

    protected virtual bool ShouldRunAiInterval()
    {
        return IsServer && !isEnemyDead;
    }

    protected virtual bool ShouldRunLateUpdate()
    {
        return IsServer && !isEnemyDead;
    }
}