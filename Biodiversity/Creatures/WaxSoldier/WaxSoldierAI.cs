using Biodiversity.Behaviours.Heat;
using Biodiversity.Core.Integration;
using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
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
        Reloading,
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
    
    // Make reload time slower as wax durability goes down?
    
    public AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> Context { get; private set; }

    private float _lastHitTime = -Mathf.Infinity;

    private static bool _hasRegisteredImperiumInsights;

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

        if (ImperiumIntegration.IsLoaded && !_hasRegisteredImperiumInsights)
        {
            bool isAgentNull = !Context.Adapter.Agent;

            Imperium.API.Visualization.InsightsFor<WaxSoldierAI>()
                .SetPersonalNameGenerator(entity => entity.BioId)
                .SetPositionOverride(entity => entity.ImperiumInsightsPanelAnchor.position)
                
                .RegisterInsight("Behaviour State", entity => entity.CurrentState.GetStateType().ToString())
                .RegisterInsight("Acceleration",
                    entity => !isAgentNull ? $"{entity.Context.Adapter.Agent.acceleration:0.0}" : "0")
                .RegisterInsight("Wax Temperature", entity => $"{entity.heatSensor.TemperatureC:0.00} °C")
                .RegisterInsight("Wax Durability", entity => $"{Mathf.Max(0, entity.Context.Blackboard.WaxDurability * 100)} %");

            _hasRegisteredImperiumInsights = true;
        }
        
        LogVerbose("Wax Soldier spawned!");
    }
    #endregion

    #region Wax Soldier Specific AI Logic
    public void UpdateWaxDurability()
    {
        WaxSoldierBlackboard bb = Context.Blackboard;
        float newDurability = bb.WaxDurability;

        if (heatSensor.TemperatureC > bb.WaxSofteningTemperature)
        {
            float t = Mathf.InverseLerp(bb.WaxSofteningTemperature, bb.WaxMeltTemperature, heatSensor.TemperatureC);

            // The Pow curve gives an accelerating melt feel 
            float bandDps = Mathf.Lerp(0f, 0.5f, Mathf.Pow(t, 2));
            
            // Add a flat damage bonus when fully melting
            float extraDps = heatSensor.TemperatureC >= bb.WaxMeltTemperature ? 0.75f : 0f;
            
            float totalDps = bandDps + extraDps;
            newDurability = bb.WaxDurability - totalDps * Time.deltaTime;
        }
        
        bb.WaxDurability = Mathf.Clamp01(Mathf.Min(bb.WaxDurability, newDurability));

        if (bb.WaxDurability <= 0 && 
            Context.Blackboard.MoltenState != MoltenState.Molten && 
            CurrentState.GetStateType() != States.Stunned)
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
        LogVerbose("Dropping musket...");
        Context.Blackboard.HeldMusket = null;
        netcodeController.DropMusketClientRpc();
    }

    /// <summary>
    /// Checks if a player is in line of sight, and updates the last known position and velocity.
    /// </summary>
    /// <returns>Whether a player is in line of sight in THIS frame.</returns>
    internal bool UpdatePlayerLastKnownPosition()
    {
        PlayerControllerB player = GetClosestVisiblePlayer(
            Context.Adapter.EyeTransform,
            Context.Blackboard.ViewWidth,
            Context.Blackboard.ViewRange,
            Context.Adapter.TargetPlayer,
            3f,
            3f);

        if (player)
        {
            Context.Adapter.TargetPlayer = player;
            Context.Blackboard.LastKnownPlayerPosition = player.transform.position;
            Context.Blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(player);
            Context.Blackboard.TimeWhenTargetPlayerLastSeen = Time.time;

            return true;
        }

        // if (Time.time - Context.Blackboard.TimeWhenTargetPlayerLastSeen >= Context.Blackboard.ThresholdTimeWherePlayerGone)
        // {
        //     Context.Adapter.TargetPlayer = null;
        //     Context.Blackboard.LastKnownPlayerPosition = Vector3.zero;
        //     Context.Blackboard.LastKnownPlayerVelocity = Vector3.zero;
        // }

        return false;
    }
    #endregion

    #region Lethal Company Vanilla Events
    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer || Context.Adapter.IsDead) return;
        
        // If the current state (fully) handles the stun event, then don't run the default reaction
        if (CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer) ?? false)
            return;
        
        if (setStunnedByPlayer)
        {
            Context.Adapter.TargetPlayer = setStunnedByPlayer;
            Context.Blackboard.LastKnownPlayerPosition = setStunnedByPlayer.transform.position;
            Context.Blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(setStunnedByPlayer);
            Context.Blackboard.TimeWhenTargetPlayerLastSeen = Time.time;
        }
        
        // Default reaction when stunned:
        SwitchBehaviourState(States.Stunned);
    }

    public override void HitEnemy(
        int force = 1,
        PlayerControllerB playerWhoHit = null,
        bool playHitSFX = false,
        int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (!IsServer || Context.Adapter.IsDead) return;

        // Hit cooldown
        if (Time.time - _lastHitTime < 0.02f)
            return;
        
        _lastHitTime = Time.time;
        
        // If friendly fire is disabled, and we weren't hit by a player, then ignore the hit
        bool isPlayerWhoHitNull = !playerWhoHit;
        if (!Context.Blackboard.IsFriendlyFireEnabled && isPlayerWhoHitNull)
            return;

        // If the current state (fully) handles the hit event, then don't run the default reaction
        if (CurrentState?.OnHitEnemy(force, playerWhoHit, hitID) ?? false)
            return;
        
        // Default reaction when hit is to start chasing the player that hit them:
        
        if (!Context.Adapter.ApplyDamage(force))
        {
            if (!isPlayerWhoHitNull)
            {
                Context.Adapter.TargetPlayer = playerWhoHit;
                Context.Blackboard.LastKnownPlayerPosition = playerWhoHit.transform.position;
                Context.Blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(playerWhoHit);
                Context.Blackboard.TimeWhenTargetPlayerLastSeen = Time.time;
                
                SwitchBehaviourState(States.Pursuing);
            }
        }
        else
        {
            SwitchBehaviourState(States.Dead);
        }
    }
    #endregion
    
    #region Little Misc Stuff
    protected override void InitializeGlobalTransitions()
    {
        base.InitializeGlobalTransitions();
        
        // GlobalTransitions.Add(new TransitionToDeadState(this));
    }
    
    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }
    
    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth.
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
        Context.Blackboard.AgentMaxSpeed = 0f;
        
        // Boost the acceleration immediately so it can decelerate quickly
        Context.Blackboard.AgentMaxAcceleration = 100;
        Context.Adapter.Agent.acceleration = Mathf.Min(Context.Adapter.Agent.acceleration * 3, Context.Blackboard.AgentMaxAcceleration);
    }

    internal void KillAllSpeed()
    {
        Context.Blackboard.AgentMaxSpeed = 0f;
        Context.Blackboard.AgentMaxAcceleration = 0f;

        Context.Adapter.Agent.speed = 0f;
        Context.Adapter.Agent.acceleration = 0f;
        Context.Adapter.Agent.velocity = Vector3.zero;
    }
    
    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        LogVerbose("Initializing config values...");
        
        WaxSoldierBlackboard bb = Context.Blackboard;
        WaxSoldierAdapter ad = Context.Adapter;
        WaxSoldierConfig cfg = WaxSoldierHandler.Instance.Config;
        
        bb.StabAttackTriggerArea = stabAttackTriggerArea;
        bb.AttackSelector = attackSelector;
        bb.NetcodeController = netcodeController;
        
        bb.ViewWidth = cfg.ViewWidth;
        bb.ViewRange = cfg.ViewRange;
        bb.IsFriendlyFireEnabled = cfg.FriendlyFire;
        bb.ThresholdTimeWherePlayerGone = new OverridableFloat(10f);
        
        ad.Health = cfg.Health;
        ad.AIIntervalLength = cfg.AiIntervalTime;
        ad.OpenDoorSpeedMultiplier = cfg.OpenDoorSpeedMultiplier;
        ad.Agent.angularSpeed = bb.AgentAngularSpeed;
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
    /// It will randomly select an audio clip from the array of clips assigned to that particular audio.
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
    /// Determines whether the played audio should be detectable by enemy AI, potentially alerting them to the player's actions.
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