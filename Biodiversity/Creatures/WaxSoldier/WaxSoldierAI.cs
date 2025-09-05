using Biodiversity.Behaviours.Heat;
using Biodiversity.Core.Integration;
using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
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
    private WaxSoldierBlackboard _blackboard => Context.Blackboard;
    private WaxSoldierAdapter _adapter => Context.Adapter;

    private float _lastHitTime = -Mathf.Infinity;

    private static bool _hasRegisteredImperiumInsights;

    #region Event Functions
    private void Awake()
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
        DebugShapeVisualizer.Clear(this);

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
                .RegisterInsight("Wax Durability",
                    entity => $"{Mathf.Max(0, entity._blackboard.WaxDurability * 100)} %");

            _hasRegisteredImperiumInsights = true;
        }

        LogVerbose("Wax Soldier spawned!");
    }
    #endregion

    #region Wax Soldier Specific AI Logic
    public void UpdateWaxDurability()
    {
        float newDurability = _blackboard.WaxDurability;

        if (heatSensor.TemperatureC > _blackboard.WaxSofteningTemperature)
        {
            float t = Mathf.InverseLerp(_blackboard.WaxSofteningTemperature, _blackboard.WaxMeltTemperature, heatSensor.TemperatureC);

            // The Pow curve gives an accelerating melt feel
            float bandDps = Mathf.Lerp(0f, 0.5f, Mathf.Pow(t, 2));

            // Add a flat damage bonus when fully melting
            float extraDps = heatSensor.TemperatureC >= _blackboard.WaxMeltTemperature ? 0.75f : 0f;

            float totalDps = bandDps + extraDps;
            newDurability = _blackboard.WaxDurability - totalDps * Time.deltaTime;
        }

        _blackboard.WaxDurability = Mathf.Clamp01(Mathf.Min(_blackboard.WaxDurability, newDurability));

        if (_blackboard.WaxDurability <= 0 &&
            _blackboard.MoltenState != MoltenState.Molten &&
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

        _blackboard.GuardPost = new Pose(calculatedPos, calculatedRot);
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
        _blackboard.HeldMusket = receivedMusket;
    }

    public void DropMusket()
    {
        LogVerbose("Dropping musket...");
        _blackboard.HeldMusket = null;
        netcodeController.DropMusketClientRpc();
    }

    /// <summary>
    /// Checks if a player is in line of sight, and updates the last known position and velocity.
    /// </summary>
    /// <returns>Whether a player is in line of sight in THIS frame.</returns>
    internal bool UpdatePlayerLastKnownPosition()
    {
        PlayerControllerB player = GetClosestVisiblePlayer(
            _adapter.EyeTransform,
            _blackboard.ViewWidth,
            _blackboard.ViewRange,
            _adapter.TargetPlayer,
            3f,
            2f);

        if (player)
        {
            _adapter.TargetPlayer = player;
            _blackboard.LastKnownPlayerPosition = player.transform.position;
            _blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(player);
            _blackboard.TimeWhenTargetPlayerLastSeen = Time.time;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates perception and switches to the appropriate behaviour state.
    /// </summary>
    /// <remarks>
    /// Delegates selection to <see cref="GetNextBehaviourStateFromPerception"/> and then calls
    /// <see cref="StateManagedAI{TState,TEnemyAI}.SwitchBehaviourState"/> with the result.
    /// </remarks>
    /// <seealso cref="GetNextBehaviourStateFromPerception"/>
    /// <seealso cref="StateManagedAI{TState,TEnemyAI}.SwitchBehaviourState"/>
    internal void UpdateBehaviourStateFromPerception()
    {
        SwitchBehaviourState(GetNextBehaviourStateFromPerception());
    }

    /// <summary>
    /// Evaluates current perception/combat context and returns the next behaviour state while it continues navigating.
    /// </summary>
    /// <remarks>
    /// Decision priority (highest first):
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///       If the player is visible, or if there is a target and the time since last seen is less
    ///       than <see cref="WaxSoldierBlackboard.PursuitLingerTime"/>, then transition to
    ///       <see cref="States.Pursuing"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Else, if there is a target and the time since last seen is less than
    ///       <see cref="WaxSoldierBlackboard.HuntingLingerTime"/>, then transition to
    ///       <see cref="States.Hunting"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Else, if the held musket has no ammo, then transition to <see cref="States.Reloading"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Otherwise, transition to <see cref="States.MovingToStation"/>.
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// This method queries visibility via <see cref="UpdatePlayerLastKnownPosition"/> and then returns the next state
    /// that it should transition to.
    /// </remarks>
    internal States GetNextBehaviourStateFromPerception()
    {
        States nextState;

        bool isPlayerVisible = UpdatePlayerLastKnownPosition();
        bool hasTarget = _adapter.TargetPlayer;
        float timeSincePlayerLastSeen = hasTarget ? _blackboard.TimeSincePlayerLastSeen : float.MaxValue;

        if (isPlayerVisible || (hasTarget && timeSincePlayerLastSeen < _blackboard.PursuitLingerTime))
        {
            nextState = States.Pursuing;
        }
        else if (hasTarget && timeSincePlayerLastSeen < _blackboard.HuntingLingerTime)
        {
            nextState = States.Hunting;
        }
        else if (_blackboard.HeldMusket.currentAmmo.Value <= 0)
        {
            nextState = States.Reloading;
        }
        else
        {
            nextState = States.MovingToStation;
        }

        return nextState;
    }
    #endregion

    #region Lethal Company Vanilla Events
    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer || _adapter.IsDead) return;

        // todo: create a wax soldier implementation of the EnemyAICollisionDetect so we can access the functions from the IShockable interface

        // If the current state (fully) handles the stun event, then don't run the default reaction
        if (CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer) ?? false)
            return;

        if (setStunnedByPlayer)
        {
            // todo: create function for these 4 duplicate lines
            _adapter.TargetPlayer = setStunnedByPlayer;
            _blackboard.LastKnownPlayerPosition = setStunnedByPlayer.transform.position;
            _blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(setStunnedByPlayer);
            _blackboard.TimeWhenTargetPlayerLastSeen = Time.time;
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
        if (!IsServer || _adapter.IsDead) return;

        // Hit cooldown
        if (Time.time - _lastHitTime < 0.02f)
            return;

        _lastHitTime = Time.time;

        // If friendly fire is disabled, and we weren't hit by a player, then ignore the hit
        bool isPlayerWhoHitNull = !playerWhoHit;
        if (!_blackboard.IsFriendlyFireEnabled && isPlayerWhoHitNull)
            return;

        // If the current state (fully) handles the hit event, then don't run the default reaction
        if (CurrentState?.OnHitEnemy(force, playerWhoHit, hitID) ?? false)
            return;

        // Default reaction when hit is to start chasing the player that hit them:

        if (!_adapter.ApplyDamage(force))
        {
            if (!isPlayerWhoHitNull)
            {
                _adapter.TargetPlayer = playerWhoHit;
                _blackboard.LastKnownPlayerPosition = playerWhoHit.transform.position;
                _blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(playerWhoHit);
                _blackboard.TimeWhenTargetPlayerLastSeen = Time.time;

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
    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }

    protected override bool ShouldRunLateUpdate()
    {
        // DebugShapeVisualizer.Clear(this);
        //
        // Vector3 origin = transform.position;
        // float lineLength = 2.5f;
        //
        // if (agent.velocity.sqrMagnitude > 0.1f)
        // {
        //     Vector3 velocityDir = agent.velocity.normalized;
        //     DebugShapeVisualizer.DrawLine(this, origin, origin + velocityDir * lineLength, Color.red);
        // }
        //
        // Vector3 parentForward = transform.forward;
        // DebugShapeVisualizer.DrawLine(this, origin, origin + parentForward * lineLength, Color.blue);
        //
        // Vector3 childForward = _adapter.Animator.gameObject.transform.forward;
        // DebugShapeVisualizer.DrawLine(this, origin, origin + childForward * lineLength, Color.green);

        return ShouldRunUpdate();
    }

    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        LogVerbose("Initializing config values...");

        WaxSoldierConfig cfg = WaxSoldierHandler.Instance.Config;

        _blackboard.StabAttackTriggerArea = stabAttackTriggerArea;
        _blackboard.AttackSelector = attackSelector;
        _blackboard.NetcodeController = netcodeController;

        _blackboard.ViewWidth = cfg.ViewWidth;
        _blackboard.ViewRange = cfg.ViewRange;
        _blackboard.IsFriendlyFireEnabled = cfg.FriendlyFire;

        _adapter.Health = cfg.Health;
        _adapter.AIIntervalLength = cfg.AiIntervalTime;
        _adapter.OpenDoorSpeedMultiplier = cfg.OpenDoorSpeedMultiplier;
        _adapter.Agent.angularSpeed = _blackboard.AgentAngularSpeed;
    }

    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierAI {BioId}]";
    }

    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Subscribing to network events...");

        netcodeController.OnSpawnMusket += HandleSpawnMusket;

        _blackboard.IsNetworkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Unsubscribing from network events...");

        netcodeController.OnSpawnMusket -= HandleSpawnMusket;

        _blackboard.IsNetworkEventsSubscribed = false;
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