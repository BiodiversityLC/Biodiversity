using Biodiversity.Util.Attributes;
using Biodiversity.Creatures.StateMachine;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

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
    
    // for compatibility with vanilla, this really shouldnt be public
    public readonly NetworkVariable<int> NetworkCurrentBehaviourStateIndex = new();
    
    /// <summary>
    /// The previous state of the AI before the current state.
    /// </summary>
    protected internal BehaviourState<TState, TEnemyAI> PreviousState;

    /// <summary>
    /// A dictionary mapping each <typeparamref name="TState"/> to its corresponding <see cref="BehaviourState{TState, TEnemyAI}"/> instance.
    /// This dictionary is populated in <see cref="ConstructStateDictionary"/> by reflecting over all types derived from <see cref="BehaviourState{TState, TEnemyAI}"/>.
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
        List<Type> stateTypes = [];
        for (int i = 0; i < BiodiversityPlugin.CachedAssemblies.Value.Count; i++)
        {
            Assembly assembly = BiodiversityPlugin.CachedAssemblies.Value[i];
            if (!assembly.FullName.Contains("Biodiversity")) continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                LogVerbose($"Failed to load types from assembly {assembly.FullName}: {e.Message}");
                continue;
            }

            for (int j = 0; j < types.Length; j++)
            {
                Type type = types[j];
                if (type.IsSubclassOf(typeof(BehaviourState<TState, TEnemyAI>)) && !type.IsAbstract)
                {
                    stateTypes.Add(type);
                }
            }
        }

        foreach (Type stateType in stateTypes)
        {

            StateAttribute attribute = stateType.GetCustomAttribute<StateAttribute>();
            if (attribute == null)
            {
                LogError($"State type {stateType.FullName} is missing a StateAttribute");
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
            ConstructorInfo[] constructors = stateType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (ConstructorInfo ctor in constructors)
            {
                LogInfo($"Constructor found for {stateType.Name}: {ctor}");
                // LogVerbose($"Constructor found for {stateType.Name}: {ctor}");
            }
            
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
                {
                    LogError($"State {stateValue} already exists in the dictionary.");
                }
                else
                {
                    LogInfo($"State {stateValue} was added successfully.");
                    // LogVerbose($"State {stateValue} was added successfully.");
                }
                    
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
            NetworkCurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
            LogVerbose($"Entering state {newState}.");
            
            CurrentState.OnStateEnter(ref initData);
            
            LogVerbose($"Successfully switched to behaviour state {newState}");
        }
        else
        {
            LogError($"State {newState} was not found in the StateDictionary. This should not happen.");
        }
    }
    
    /// <summary>
    /// Requests the server to play a specific category of audio clip on a designated <see cref="UnityEngine.AudioSource"/>.
    /// It will randomly select an audio clip from the array of clips assigned to that particular audio  .
    /// This method ensures that the selected audio clip is synchronized across all clients.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Laugh", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="UnityEngine.AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSFX", "Footsteps").
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="UnityEngine.AudioSource"/> should be interrupted 
    /// before playing the new audio clip.
    /// </param>
    /// <param name="audibleInWalkieTalkie">
    /// Indicates whether the played audio should be transmitted through the walkie-talkie system, making it audible 
    /// to players using walkie-talkies.
    /// </param>
    /// <param name="audibleByEnemies">
    /// Determines whether the played audio should be detectable by enemy AI, potentially alerting them to the player's 
    /// actions.
    /// </param>
    /// <param name="slightlyVaryPitch">
    /// Whether to slightly vary the pitch between 0.9 and 1.1 randomly.
    /// </param>
    [ServerRpc]
    internal void PlayRandomAudioClipTypeServerRpc(
        string audioClipType,
        string audioSourceType,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false,
        bool slightlyVaryPitch = false)
    {
        // Validate audio clip type
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr))
        {
            LogError($"Audio Clip Type '{audioClipType}' not defined for {GetType().Name}.");
            return;
        }

        int numberOfClips = clipArr.Length;

        if (numberOfClips == 0)
        {
            LogError($"No audio clips available for type '{audioClipType}' in {GetType().Name}.");
            return;
        }

        // Validate audio source type
        if (!AudioSources.ContainsKey(audioSourceType))
        {
            LogError($"Audio Source Type '{audioSourceType}' not defined for {GetType().Name}.");
            return;
        }

        // Select a random clip index
        int clipIndex = UnityEngine.Random.Range(0, numberOfClips);
        PlayAudioClipTypeClientRpc(audioClipType, audioSourceType, clipIndex, interrupt, audibleInWalkieTalkie,
            audibleByEnemies, slightlyVaryPitch);
    }

    /// <summary>
    /// Plays the selected audio clip on the specified <see cref="UnityEngine.AudioSource"/> across all clients.
    /// This method is invoked by the server to ensure synchronized audio playback.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Chase", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="UnityEngine.AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSfx", "Footsteps").
    /// </param>
    /// <param name="clipIndex">
    /// The index of the <see cref="AudioClip"/> within the array corresponding to <paramref name="audioClipType"/> 
    /// that should be played.
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="UnityEngine.AudioSource"/> should be interrupted 
    /// before playing the new audio clip.
    /// </param>
    /// <param name="audibleInWalkieTalkie">
    /// Indicates whether the played audio should be transmitted through the walkie-talkie system, making it audible 
    /// to players using walkie-talkies.
    /// </param>
    /// <param name="audibleByEnemies">
    /// Determines whether the played audio should be detectable by enemy AI, potentially alerting them to the player's 
    /// actions.
    /// </param>
    /// <param name="slightlyVaryPitch">
    /// Whether to slightly vary the pitch between 0.9 and 1.1 randomly.
    /// </param>
    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(
        string audioClipType,
        string audioSourceType,
        int clipIndex,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false,
        bool slightlyVaryPitch = false)
    {
        // Validate audio clip type
        if (!AudioClips.ContainsKey(audioClipType))
        {
            LogError($"Audio Clip Type '{audioClipType}' not defined on client for {GetType().Name}.");
            return;
        }

        // Validate audio source type
        if (!AudioSources.ContainsKey(audioSourceType))
        {
            LogError($"Audio Source Type '{audioSourceType}' not defined on client for {GetType().Name}.");
            return;
        }

        AudioClip[] clips = AudioClips[audioClipType];
        if (clipIndex < 0 || clipIndex >= clips.Length)
        {
            LogError($"Invalid clip index {clipIndex} for type '{audioClipType}' in {GetType().Name}.");
            return;
        }

        AudioClip clipToPlay = clips[clipIndex];
        if (clipToPlay == null)
        {
            LogError($"Audio clip at index {clipIndex} for type '{audioClipType}' is null in {GetType().Name}.");
            return;
        }

        AudioSource selectedAudioSource = AudioSources[audioSourceType];
        if (selectedAudioSource == null)
        {
            LogError($"Audio Source '{audioSourceType}' is null in {GetType().Name}.");
            return;
        }

        LogDebug(
            $"Playing audio clip: {clipToPlay.name} for type '{audioClipType}' on AudioSource '{audioSourceType}' in {GetType().Name}.");

        if (interrupt) selectedAudioSource.Stop();
        if (slightlyVaryPitch) selectedAudioSource.pitch = UnityEngine.Random.Range(selectedAudioSource.pitch - 0.1f, selectedAudioSource.pitch + 0.1f);
        
        selectedAudioSource.PlayOneShot(clipToPlay);
        
        if (audibleInWalkieTalkie)
            WalkieTalkie.TransmitOneShotAudio(selectedAudioSource, clipToPlay, selectedAudioSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(selectedAudioSource.transform.position);
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