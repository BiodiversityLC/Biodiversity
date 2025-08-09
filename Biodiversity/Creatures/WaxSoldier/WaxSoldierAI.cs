using Biodiversity.Behaviours.Heat;
using Biodiversity.Core.Integration;
using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAI : StateManagedAI<WaxSoldierAI.States, WaxSoldierAI>
{
#pragma warning disable 0649
    [Header("Transforms")]
    [SerializeField] private Transform ImperiumInsightsPanelAnchor;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private BoxCollider stabAttackTriggerArea;
    [SerializeField] private AttackSelector attackSelector;
    [SerializeField] private HeatSensor heatSensor;
    [SerializeField] public WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649
    
    public enum States
    {
        Spawning,
        MovingToStation,
        ArrivingAtStation,
        Stationary,
        Pursuing,
        Attacking,
        Hunting,
        TransformingToMolten,
        Stunned,
        Dead,
    }

    public enum MoltenState
    {
        Unmolten,
        Molten
    }
    /* Molten state ideas:
     *
     * Maybe he can break doors off its hinges like the fiend
     * Sound triangulation
     * Ambush attacks (figure out ambush points by considering where scrap is, apparatus, etc), but don't do cheap annoying stuff like guarding the entrance to the dungeon
     */
    
    public AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> Context { get; private set; }

    #region Event Functions
    public void Awake()
    {
        WaxSoldierBlackboard blackboard = new();
        WaxSoldierAdapter adapter = new(this);
        
        PlayerTargetableConditions.AddCondition(player => !PlayerUtil.IsPlayerDead(player));

        Context = new AIContext<WaxSoldierBlackboard, WaxSoldierAdapter>(blackboard, adapter);
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void OnDestroy()
    {
        DropMusket();
        base.OnDestroy();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        CollectAudioClipsAndSources<WaxSoldierClient>();
        SubscribeToNetworkEvents();
        InitializeConfigValues();

        if (ImperiumIntegration.IsLoaded)
        {
            bool isAgentNull = !Context.Adapter.Agent;

            Imperium.API.Visualization.InsightsFor<WaxSoldierAI>()
                .UnregisterInsight("Movement Speed")
                .UnregisterInsight("Location")
                
                .SetPersonalNameGenerator(entity => entity.BioId)
                .SetPositionOverride(entity => entity.ImperiumInsightsPanelAnchor.position)
                
                .RegisterInsight("Behaviour State", entity => entity.CurrentState.GetStateType().ToString())
                .RegisterInsight("Speed",
                    entity => !isAgentNull ? $"{entity.Context.Adapter.Agent.speed:0.0}" : "0")
                .RegisterInsight("Acceleration",
                    entity => !isAgentNull ? $"{entity.Context.Adapter.Agent.acceleration:0.0}" : "0")
                .RegisterInsight("Wax Temperature", entity => $"{entity.Context.Blackboard.WaxTemperature:0.00} °C")
                .RegisterInsight("Wax Durability", entity => $"{entity.Context.Blackboard.WaxDurability * 100} %");
        }
        
        LogVerbose("Wax Soldier spawned!");
    }
    #endregion

    #region Wax Soldier Specific AI Logic
    protected override void InitializeGlobalTransitions()
    {
        base.InitializeGlobalTransitions();
        
        GlobalTransitions.Add(new TransitionToDeadState(this));
    }
    
    public void UpdateHeat()
    {
        WaxSoldierBlackboard bb = Context.Blackboard;
        float dt = Time.deltaTime;
        float heatRatePerSec = heatSensor.heatRate;
        
        float coolingFactor = Mathf.Exp(-dt / bb.CoolingTimeConstant);
        bb.WaxTemperature = bb.WaxTemperature * coolingFactor + bb.AmbientTemperature * (1f - coolingFactor);
        bb.WaxTemperature += heatRatePerSec * dt;
        
        float targetDurability;
        if (bb.WaxTemperature <= bb.WaxSofteningTemperature) targetDurability = 1f;
        else if (bb.WaxTemperature >= bb.WaxMeltTemperature) targetDurability = 0f;
        else
        {
            float midTemperature = 0.5f * (bb.WaxSofteningTemperature + bb.WaxMeltTemperature);
            targetDurability = 1f / (1f + Mathf.Exp(0.35f * (bb.WaxTemperature - midTemperature)));
        }

        float lerpFactor = 1f - Mathf.Exp(-dt / 5f);
        bb.WaxDurability = Mathf.Clamp01(Mathf.Lerp(bb.WaxDurability, targetDurability, lerpFactor));

        if (bb.WaxDurability <= 0 && Context.Blackboard.MoltenState != MoltenState.Molten && CurrentState.GetStateType() != States.Stunned)
        {
            SwitchBehaviourState(States.TransformingToMolten);
        }
    }

    public void DetermineGuardPostPosition()
    {
        //todo: create tool that lets people easily select good guard spots for the wax soldier (nearly identical to the vending machine placement tool idea)
        
        // for now lets just use this
        Vector3 tempGuardPostPosition = GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position;
        
        Vector3 calculatedPos = tempGuardPostPosition;
        Quaternion calculatedRot = transform.rotation;

        Context.Blackboard.GuardPost = new Pose(calculatedPos, calculatedRot);
    }
    
    private void HandleSpawnMusket(NetworkObjectReference objectReference, int scrapValue)
    {
        if (!IsServer) return;
        
        if (!objectReference.TryGet(out NetworkObject networkObject))
        {
            LogError("Received null network object for the musket.");
            return;
        }

        if (!networkObject.TryGetComponent(out Musket receivedMusket))
        {
            LogError("The musket component on the musket network object is null.");
            return;
        }

        LogVerbose("Musket spawned successfully.");
        Context.Blackboard.HeldMusket = receivedMusket;
    }

    public void DropMusket()
    {
        Context.Blackboard.HeldMusket = null;
        netcodeController.DropMusketClientRpc();
    }
    #endregion

    #region Lethal Company Vanilla Events
    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        
        States curState = CurrentState.GetStateType();
        if (curState != States.Stunned && curState != States.Dead && curState != States.Spawning)
            SwitchBehaviourState(States.Stunned);
        
        // CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
    }

    public override void HitEnemy(
        int force = 1,
        PlayerControllerB playerWhoHit = null,
        bool playHitSFX = false,
        int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (!IsServer) return;
        CurrentState?.OnHitEnemy(force, playerWhoHit, hitID);
    }
    #endregion
    
    #region Little Misc Stuff
    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }
    
    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth
    /// </summary>
    internal void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        Context.Adapter.Agent.speed = Mathf.Lerp(Context.Adapter.Agent.speed, Context.Blackboard.AgentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        Context.Adapter.Agent.acceleration = Mathf.Lerp(Context.Adapter.Agent.acceleration, Context.Blackboard.AgentMaxAcceleration, accelerationAdjustment);
    }

    internal void DecelerateAndStop()
    {
        // Make the agent's speed smoothly go to zero
        Context.Blackboard.AgentMaxSpeed = 0;
        
        // Boost the acceleration immediately so it can decelerate quickly
        Context.Adapter.Agent.acceleration *= 3;
    }
    
    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        LogVerbose("Initializing config values...");
        
        Context.Blackboard.ViewWidth = WaxSoldierHandler.Instance.Config.ViewWidth;
        Context.Blackboard.ViewRange = WaxSoldierHandler.Instance.Config.ViewRange;
        Context.Blackboard.AgentAngularSpeed = 250f;
        Context.Blackboard.WaxDurability = 1f;
        Context.Blackboard.WaxTemperature = 20f;
        Context.Blackboard.AmbientTemperature = 20f;
        Context.Blackboard.WaxSofteningTemperature = 40f;
        Context.Blackboard.WaxMeltTemperature = 60f;
        
        Context.Blackboard.StabAttackTriggerArea = stabAttackTriggerArea;
        Context.Blackboard.AttackSelector = attackSelector;
        
        Context.Adapter.Health = WaxSoldierHandler.Instance.Config.Health;
        Context.Adapter.AIIntervalLength = WaxSoldierHandler.Instance.Config.AiIntervalTime;
        Context.Adapter.OpenDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        Context.Adapter.Agent.angularSpeed = Context.Blackboard.AgentAngularSpeed;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierAI {BioId}]";
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || Context.Blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Subscribing to network events...");
        
        netcodeController.OnSpawnMusket += HandleSpawnMusket;
        
        Context.Blackboard.IsNetworkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !Context.Blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Unsubscribing from network events...");
        
        netcodeController.OnSpawnMusket -= HandleSpawnMusket;
        
        Context.Blackboard.IsNetworkEventsSubscribed = false;
    }
    #endregion
    
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
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr) || clipArr == null || clipArr.Length == 0)
        {
            LogWarning($"Audio Clip Type '{audioClipType}' not found, is null, or empty.");
            return;
        }

        // Validate audio source type
        if (!AudioSources.TryGetValue(audioSourceType, out AudioSource source) || !source)
        {
            LogWarning($"Audio Source Type '{audioSourceType}' not found or null.");
            return;
        }

        // Select a random clip index
        int clipIndex = Random.Range(0, clipArr.Length);
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
    /// Whether to slightly vary the pitch between the original pitch +/- 0.1.
    /// </param>
    [ClientRpc]
    internal void PlayAudioClipTypeClientRpc(
        string audioClipType,
        string audioSourceType,
        int clipIndex,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false,
        bool slightlyVaryPitch = false)
    {
        // Validate audio clip type
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr) || clipArr == null || clipArr.Length == 0)
        {
            LogWarning($"Client: Audio Clip Type '{audioClipType}' not found, is null, or empty.");
            return;
        }
        
        if (clipIndex < 0 || clipIndex >= clipArr.Length)
        {
            LogWarning($"Client: Invalid clip index {clipIndex} received for type '{audioClipType}' (Count: {clipArr.Length}).");
            return;
        }

        AudioClip clipToPlay = clipArr[clipIndex];
        if (!clipToPlay)
        {
            LogWarning($"Client: Audio clip at index {clipIndex} for type '{audioClipType}' is null.");
            return;
        }
        
        if (!AudioSources.TryGetValue(audioSourceType, out AudioSource selectedAudioSource) || selectedAudioSource == null)
        {
            LogWarning($"Client: Audio Source Type '{audioSourceType}' not found or is null.");
            return;
        }

        LogVerbose(
            $"Client: Playing audio clip: {clipToPlay.name} for type '{audioClipType}' on AudioSource '{audioSourceType}'.");

        float oldPitch = selectedAudioSource.pitch;
        
        if (interrupt && selectedAudioSource.isPlaying) selectedAudioSource.Stop();
        if (slightlyVaryPitch) selectedAudioSource.pitch = Random.Range(oldPitch - 0.1f, oldPitch + 0.1f);
        
        selectedAudioSource.PlayOneShot(clipToPlay);
        
        if (audibleInWalkieTalkie) WalkieTalkie.TransmitOneShotAudio(selectedAudioSource, clipToPlay, selectedAudioSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(selectedAudioSource.transform.position);

        if (slightlyVaryPitch) selectedAudioSource.pitch = oldPitch;
    }
}

// https://discord.com/channels/1168655651455639582/1225942840282976316/1299356591795077131