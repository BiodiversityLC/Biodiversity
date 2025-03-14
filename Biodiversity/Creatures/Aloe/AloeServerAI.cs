using System.Collections;
using System.Collections.Generic;
using Biodiversity.Creatures.Aloe.BehaviourStates;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeServerAI : StateManagedAI<AloeServerAI.AloeStates, AloeServerAI>
{
    public AISearchRoutine roamMap;
    
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    public int PlayerHealthThresholdForHealing { get; private set; } = 45;
    public float ViewWidth { get; private set; } = 135f;
    public int ViewRange { get; private set; } = 65;
    public float PassiveStalkStaredownDistance { get; private set; } = 10f;
    public float TimeItTakesToFullyHealPlayer { get; private set; } = 15f;
    public float WaitBeforeChasingEscapedPlayerTime { get; private set; } = 2f;
    
#pragma warning disable 0649
    [Header("Transforms")] [Space(5f)] 
    [SerializeField] private Transform rootTransform;
    [SerializeField] private Transform headBone;

    [Header("Colliders")] [Space(5f)]
    [SerializeField] private Collider bodyCollider;
    [SerializeField] private SphereCollider slapCollider;
    
    [Header("Controllers")] [Space(5f)] 
    public AloeNetcodeController netcodeController;
#pragma warning restore 0649
    
    public enum AloeStates
    {
        Spawning,
        Roaming,
        AvoidingPlayer,
        PassiveStalking,
        AggressiveStalking,
        KidnappingPlayer,
        HealingPlayer,
        CuddlingPlayer,
        ChasingEscapedPlayer, 
        AttackingPlayer,
        Dead,
    }
    
    /// <summary>
    /// A dictionary containing arrays of <see cref="AudioClip"/>s, indexed by category name.
    /// </summary>
    private Dictionary<string, AudioClip[]> AudioClips { get; set; } = new();
    
    /// <summary>
    /// A dictionary containing <see cref="AudioSource"/> components, indexed by category name.
    /// </summary>
    private Dictionary<string, AudioSource> AudioSources { get; set; } = new();

    internal readonly NullableObject<PlayerControllerB> ActualTargetPlayer = new();
    internal readonly NullableObject<PlayerControllerB> AvoidingPlayer = new();
    internal readonly NullableObject<PlayerControllerB> SlappingPlayer = new();
    internal PlayerControllerB BackupTargetPlayer;
    
    internal Vector3 FavouriteSpot;
    private Vector3 _mainEntrancePosition;
    
    internal float AgentMaxAcceleration;
    internal float AgentMaxSpeed;
    private float _takeDamageCooldown;

    internal int TimesFoundSneaking;

    internal bool HasTransitionedToRunningForwardsAndCarryingPlayer;
    internal bool IsStaringAtTargetPlayer;
    internal bool InSlapAnimation;
    [HideInInspector] public bool inCrushHeadAnimation;
    private bool _networkEventsSubscribed;
    private bool _inStunAnimation;

    private void OnEnable()
    {
        if (!IsServer) return;
        SubscribeToNetworkEvents();
    }
    
    private void OnDisable()
    {
        if (!IsServer) return;
        
        SetTargetPlayerInCaptivity(false);
        AloeSharedData.Instance.UnOccupyBrackenRoomAloeNode(FavouriteSpot);
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        SubscribeToNetworkEvents();
        
        netcodeController.SyncAloeIdClientRpc(BioId);
        agent.updateRotation = false;
        
        CollectAudioClipsAndSources();
        
        PlayerTargetableConditions.AddCondition(player => player.isInsideFactory);
        PlayerTargetableConditions.AddCondition(player => !(player.sinkingValue >= 0.7300000190734863));
        PlayerTargetableConditions.AddCondition(player => !AloeSharedData.Instance.IsPlayerKidnapBound(player));
        
        LogVerbose("Aloe Spawned!");
    }
    
    protected override AloeStates DetermineInitialState()
    {
        return AloeStates.Spawning;
    }

    protected override string GetLogPrefix()
    {
        return $"[AloeServerAI {BioId}]";
    }

    protected override bool ShouldRunUpdate()
    {
        if (!IsServer || isEnemyDead || StartOfRound.Instance.livingPlayers == 0) 
            return false;

        _takeDamageCooldown -= Time.deltaTime;
        
        CalculateSpeed();
        CalculateRotation();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation)
        {
            netcodeController.AnimationParamStunned.Value = false;
            _inStunAnimation = false;
        }
        
        if (_inStunAnimation || InSlapAnimation || inCrushHeadAnimation)
            return false;

        return true;
    }

    protected override bool ShouldRunAiInterval()
    {
        return IsServer && !isEnemyDead && StartOfRound.Instance.livingPlayers > 0 && !_inStunAnimation && !InSlapAnimation;
    }

    protected override bool ShouldRunLateUpdate()
    {
        return ShouldRunAiInterval();
    }
    
    /// <summary>
    /// Collects all <see cref="AudioClip"/> and <see cref="AudioSource"/> fields defined on the object.
    /// Then it populates the <see cref="AudioClips"/> and <see cref="AudioSources"/> dictionaries based on field names and values.
    /// </summary>
    private void CollectAudioClipsAndSources()
    {
        AudioClips = new Dictionary<string, AudioClip[]>();
        AudioSources = new Dictionary<string, AudioSource>();

        AloeClient aloeClient = GetComponent<AloeClient>();

        for (int i = 0;
             i < typeof(AloeClient).GetFields(
                 BindingFlags.Public |
                 BindingFlags.NonPublic |
                 BindingFlags.Instance |
                 BindingFlags.DeclaredOnly).Length;
             i++)
        {
            FieldInfo field = typeof(AloeClient).GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.DeclaredOnly)[i];
            string key = field.Name;
            Type fieldType = field.FieldType;

            LogVerbose($"Field name: {key}, field type: {fieldType}");

            // This code looks ugly because there are null checks inside each if statement.
            // Its better this way though because we only null check if we find a type that we actually want first.
            if (fieldType == typeof(AudioClip[]))
            {
                AudioClip[] value = (AudioClip[])field.GetValue(aloeClient);
                if (value is { Length: > 0 }) AudioClips[key] = value;
            }
            else if (fieldType == typeof(AudioClip))
            {
                AudioClip value = (AudioClip)field.GetValue(aloeClient);
                if (value != null) AudioClips[key] = [value];
            }
            else if (fieldType == typeof(AudioSource))
            {
                AudioSource value = (AudioSource)field.GetValue(aloeClient);
                if (value != null) AudioSources[key] = value;
            }
        }
    }

    /// <summary>
    /// Requests the server to play a specific category of audio clip on a designated <see cref="AudioSource"/>.
    /// It will randomly select an audio clip from the array of clips assigned to that particular audio  .
    /// This method ensures that the selected audio clip is synchronized across all clients.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Laugh", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSFX", "Footsteps").
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="AudioSource"/> should be interrupted 
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
        int clipIndex = Random.Range(0, numberOfClips);
        PlayAudioClipTypeClientRpc(audioClipType, audioSourceType, clipIndex, interrupt, audibleInWalkieTalkie,
            audibleByEnemies, slightlyVaryPitch);
    }

    /// <summary>
    /// Plays the selected audio clip on the specified <see cref="AudioSource"/> across all clients.
    /// This method is invoked by the server to ensure synchronized audio playback.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Chase", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSfx", "Footsteps").
    /// </param>
    /// <param name="clipIndex">
    /// The index of the <see cref="AudioClip"/> within the array corresponding to <paramref name="audioClipType"/> 
    /// that should be played.
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="AudioSource"/> should be interrupted 
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
        if (slightlyVaryPitch) selectedAudioSource.pitch = Random.Range(selectedAudioSource.pitch - 0.1f, selectedAudioSource.pitch + 0.1f);
        
        selectedAudioSource.PlayOneShot(clipToPlay);
        
        if (audibleInWalkieTalkie)
            WalkieTalkie.TransmitOneShotAudio(selectedAudioSource, clipToPlay, selectedAudioSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(selectedAudioSource.transform.position);
    }

    public void PickFavouriteSpot()
    {
        if (!IsServer) return;
        
        _mainEntrancePosition = RoundManager.FindMainEntrancePosition(true);
        Vector3 brackenRoomAloeNode = AloeSharedData.Instance.OccupyBrackenRoomAloeNode();

        // Check if the Aloe is outside, and if she is then teleport her back inside
        {
            Vector3 enemyPos = transform.position;
            Vector3 closestOutsideNode = Vector3.positiveInfinity;
            Vector3 closestInsideNode = Vector3.positiveInfinity;
            
            IEnumerable<Vector3> insideNodePositions = AloeUtils.FindInsideAINodePositions();
            IEnumerable<Vector3> outsideNodePositions = AloeUtils.FindOutsideAINodePositions();

            foreach (Vector3 pos in outsideNodePositions)
            {
                if ((pos - enemyPos).sqrMagnitude < (closestOutsideNode - enemyPos).sqrMagnitude)
                {
                    closestOutsideNode = pos;
                }
            }
        
            foreach (Vector3 pos in insideNodePositions)
            {
                if ((pos - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
                {
                    closestInsideNode = pos;
                }
            }

            if ((closestOutsideNode - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
            {
                allAINodes = AloeSharedData.Instance.GetOutsideAINodes();
            }
        }

        //_favouriteSpot = brackenRoomAloeNode;
        FavouriteSpot = Vector3.zero;
        if (FavouriteSpot == Vector3.zero)
        {
            FavouriteSpot =
                GetFarthestValidNodeFromPosition(
                        out PathStatus _,
                        agent,
                        _mainEntrancePosition != Vector3.zero ? _mainEntrancePosition : transform.position,
                        allAINodes,
                        bufferDistance: 0f)
                    .position;
        }
        
        LogVerbose($"Found a favourite spot: {FavouriteSpot}");
    }
    
    /// <summary>
    /// Calculates the rotation for the Aloe manually, which is needed because of the kidnapping animation
    /// </summary>
    private void CalculateRotation()
    {
        const float turnSpeed = 5f;

        if (inCrushHeadAnimation) return;
        if (InSlapAnimation && SlappingPlayer.IsNotNull)
        {
            LookAtPosition(SlappingPlayer.Value.transform.position);
        }

        switch (CurrentState.GetStateType())
        {
            case AloeStates.Dead or AloeStates.HealingPlayer or AloeStates.CuddlingPlayer:
                break;
            
            default:
            {
                if (!(agent.velocity.sqrMagnitude > 0.01f)) break;
                Vector3 targetDirection = !HasTransitionedToRunningForwardsAndCarryingPlayer &&
                                          CurrentState.GetStateType() == AloeStates.KidnappingPlayer
                    ? -agent.velocity.normalized
                    : agent.velocity.normalized;
        
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
                break;
            }
        }
    }

    public IEnumerator TransitionToRunningForwardsAndCarryingPlayer(float transitionDuration)
    {
        Vector3 forwardDirection = agent.velocity.normalized;
        Quaternion initialRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(forwardDirection);
        netcodeController.TransitionToRunningForwardsAndCarryingPlayerClientRpc(BioId, transitionDuration);
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            yield return null;
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
        }

        transform.rotation = targetRotation;
        HasTransitionedToRunningForwardsAndCarryingPlayer = true;
        AgentMaxSpeed = AloeHandler.Instance.Config.KidnappingPlayerCarryingMaxSpeed;
        AgentMaxAcceleration = AloeHandler.Instance.Config.KidnappingPlayerCarryingMaxAcceleration;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        switch (CurrentState.GetStateType())
        {
            case AloeStates.ChasingEscapedPlayer:
            {
                if (((ChasingEscapedPlayerState)CurrentState).WaitBeforeChasingTimer > 0) break;
                
                LogVerbose("Player is touching the aloe! Kidnapping him now.");
                netcodeController.SetAnimationTriggerClientRpc(BioId, AloeClient.Grab);
                SwitchBehaviourState(AloeStates.KidnappingPlayer);
                break;
            }

            case AloeStates.AttackingPlayer:
            {
                LogVerbose("Player is touching the aloe! Killing them!");
                netcodeController.CrushPlayerClientRpc(BioId, ActualTargetPlayer.Value.actualClientId);
                SwitchBehaviourState(AloeStates.ChasingEscapedPlayer);
                break;
            }
        }
    }

    /// <summary>
    /// The function for handling when the Aloe gets hit.
    /// This function is from the base EnemyAI class.
    /// </summary>
    /// <param name="force">The amount of damage that was done by the hit.</param>
    /// <param name="playerWhoHit">The player object that hit the Aloe, if it was hit by a player.</param>
    /// <param name="playHitSFX">Don't use this.</param>
    /// <param name="hitId">The ID of hit which dealt the damage.</param>
    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
        if (!IsServer) return;
        if (isEnemyDead) return;
        
        AloeStates currentAloeStateType = CurrentState.GetStateType();
        if (currentAloeStateType is AloeStates.Dead || _takeDamageCooldown > 0) return;

        PlayRandomAudioClipTypeServerRpc(AloeClient.AudioClipTypes.hitSfx.ToString(), "creatureVoice", false, true, false, true);
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        
        if (enemyHP > 0)
        {
            if (currentAloeStateType is AloeStates.Spawning) return;

            NullableObject<PlayerControllerB> playerWhoHitMe = new(playerWhoHit);
            
            StateData stateData = new();
            stateData.Add("overridePlaySpottedAnimation", true);
            
            switch (currentAloeStateType)
            {
                case AloeStates.Roaming or AloeStates.AvoidingPlayer or AloeStates.PassiveStalking or AloeStates.AggressiveStalking:
                {
                    if (playerWhoHitMe.IsNotNull)
                    {
                        if (currentAloeStateType is not (AloeStates.Roaming or AloeStates.AvoidingPlayer) || enemyHP <= AloeHandler.Instance.Config.Health / 2)
                        {
                            LogVerbose("Triggering bitch slap.");
                            SlappingPlayer.Value = playerWhoHitMe.Value;
                            netcodeController.SetAnimationTriggerClientRpc(BioId, AloeClient.Slap);
                        }
                        else 
                            LogVerbose($"Did not trigger bitch slap. Current health: {enemyHP}. Health needed to trigger slap: {AloeHandler.Instance.Config.Health / 2}");
                        
                        AvoidingPlayer.Value = playerWhoHitMe.Value;
                        stateData.Add("hitByEnemy", false);
                    }
                    else
                    {
                        if (force <= 0) return;
                        stateData.Add("hitByEnemy", true);
                    }
                    
                    if (currentAloeStateType is not AloeStates.AvoidingPlayer) SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                    
                    break;
                }
                
                case AloeStates.KidnappingPlayer or AloeStates.HealingPlayer or AloeStates.CuddlingPlayer:
                {
                    if (playerWhoHitMe.IsNotNull)
                    {
                        SetTargetPlayerInCaptivity(false);
                        BackupTargetPlayer = ActualTargetPlayer.Value;
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                        SwitchBehaviourState(AloeStates.AttackingPlayer);
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        SetTargetPlayerInCaptivity(false);
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                    }
                
                    break;
                }

                case AloeStates.ChasingEscapedPlayer:
                {
                    if (playerWhoHitMe.IsNotNull)
                    {
                        BackupTargetPlayer = ActualTargetPlayer.Value;
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                        SwitchBehaviourState(AloeStates.AttackingPlayer);
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                    }
                    
                    break;
                }
                
                case AloeStates.AttackingPlayer:
                {
                    if (playerWhoHitMe.IsNotNull && ActualTargetPlayer.Value != playerWhoHitMe.Value)
                    {
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                    }
                
                    break;
                }
            }
        }
        else
        {
            SwitchBehaviourState(AloeStates.Dead);
        }
    }

    /// <summary>
    /// The function for handling when the Aloe gets stunned.
    /// This function is from the base EnemyAI class.
    /// </summary>
    /// <param name="setToStunned">Not really sure what this is for.</param>
    /// <param name="setToStunTime">The time that the aloe is going to be stunned for.</param>
    /// <param name="setStunnedByPlayer">The player that the Aloe was stunned by.</param>
    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        if (isEnemyDead) return;
        
        AloeStates currentAloeState = CurrentState.GetStateType();
        if (currentAloeState is AloeStates.Dead) return;

        _inStunAnimation = true;
        PlayRandomAudioClipTypeServerRpc(AloeClient.AudioClipTypes.stunSfx.ToString(), creatureVoice.ToString(), true, true, false, true);
        netcodeController.AnimationParamStunned.Value = true;
        netcodeController.AnimationParamHealing.Value = false;

        StateData stateData = new();
        stateData.Add("overridePlaySpottedAnimation", true);
        
        NullableObject<PlayerControllerB> stunnedByPlayer2 = new(setStunnedByPlayer);
        switch (currentAloeState)
        { 
            case AloeStates.Spawning or AloeStates.Roaming or AloeStates.PassiveStalking or AloeStates.AggressiveStalking:
            {
                if (stunnedByPlayer2.IsNotNull) AvoidingPlayer.Value = stunnedByPlayer2.Value;
                
                SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                break;
            }
            
            case AloeStates.KidnappingPlayer or AloeStates.HealingPlayer or AloeStates.CuddlingPlayer:
            {
                SetTargetPlayerInCaptivity(false);
                if (stunnedByPlayer2.IsNotNull)
                {
                    BackupTargetPlayer = ActualTargetPlayer.Value;
                    netcodeController.TargetPlayerClientId.Value = stunnedByPlayer2.Value.actualClientId;
                    SwitchBehaviourState(AloeStates.AttackingPlayer);
                }
                else
                {
                    AvoidingPlayer.Value = null;
                    SwitchBehaviourState(AloeStates.AvoidingPlayer, initData: stateData);
                }
                
                break;
            }
            
            case AloeStates.AttackingPlayer:
            {
                if (!stunnedByPlayer2.IsNotNull) break;

                if (ActualTargetPlayer.Value != setStunnedByPlayer)
                    netcodeController.TargetPlayerClientId.Value = stunnedByPlayer2.Value.actualClientId;
                
                break;
            }
        }
    }
    
    /// <summary>
    /// Creates a bind in the AloeBoundKidnaps dictionary and calls a network event to do several things in the client for kidnapping the target player.
    /// </summary>
    /// <param name="setToInCaptivity">Whether the target player is being kidnapped or finished being kidnapped.</param>
    public void SetTargetPlayerInCaptivity(bool setToInCaptivity)
    {
        if (!IsServer) return;
        if (!ActualTargetPlayer.IsNotNull) return;
        if (setToInCaptivity)
        {
            if (!AloeSharedData.Instance.IsAloeKidnapBound(this))
                AloeSharedData.Instance.Bind(this, ActualTargetPlayer.Value, BindType.Kidnap);
        }
        else 
        {
            if (AloeSharedData.Instance.IsAloeKidnapBound(this))
                AloeSharedData.Instance.Unbind(this, BindType.Kidnap);
        }
        
        netcodeController.SetTargetPlayerInCaptivityClientRpc(BioId, setToInCaptivity);
    }

    /// <summary>
    /// Is called by the teleporter patch to make sure the aloe reacts appropriately when a player is teleported away
    /// </summary>
    public void SetTargetPlayerEscapedByTeleportation()
    {
        if (!IsServer) return;
        if (!ActualTargetPlayer.IsNotNull)
        {
            LogWarning($"{nameof(SetTargetPlayerEscapedByTeleportation)} called, but the target player object is null.");
            return;
        }
        
        AloeStates localCurrentAloeState = CurrentState.GetStateType();
        if (localCurrentAloeState is not (AloeStates.KidnappingPlayer or AloeStates.CuddlingPlayer or AloeStates.HealingPlayer)) return;
        
        LogVerbose("Target player escaped by teleportation!");
        if (AloeSharedData.Instance.IsPlayerStalkBound(ActualTargetPlayer.Value))
            AloeSharedData.Instance.Unbind(this, BindType.Stalk);
        SetTargetPlayerInCaptivity(false);
            
        netcodeController.TargetPlayerClientId.Value = NullPlayerId;
        SwitchBehaviourState(AloeStates.Roaming);
    }
    
    /// <summary>
    /// Finds and returns the player that is closest to the specified transform, considering a buffer distance.
    /// </summary>
    /// <param name="players">The list of players to search through.</param>
    /// <param name="position">The vector to measure distances from.</param>
    /// <param name="currentTargetPlayer">The current player being targeted.</param>
    /// <param name="bufferDistance">The buffer distance to prevent constant target switching.</param>
    /// <returns>The player that is closest to the specified transform within the buffer distance, or the closest player if none are within the buffer distance.</returns>
    internal PlayerControllerB GetClosestPlayerFromListConsideringTargetPlayer(
        List<PlayerControllerB> players, 
        Vector3 position,
        PlayerControllerB currentTargetPlayer, 
        float bufferDistance = 1.5f)
    {
        PlayerControllerB closestPlayer = currentTargetPlayer;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerControllerB player = players[i];
            if (player == currentTargetPlayer) continue; // Skip the target player itself
            float distance = Vector3.Distance(position, player.transform.position);
            if (!(distance < closestDistance)) continue;
            closestDistance = distance;
            closestPlayer = player;
        }

        if (currentTargetPlayer == null) return closestPlayer;
        float currentTargetPlayerDistance = Vector3.Distance(position, currentTargetPlayer.transform.position);
        return Mathf.Abs(closestDistance - currentTargetPlayerDistance) < bufferDistance ? currentTargetPlayer : closestPlayer;
    }

    /// <summary>
    /// Is called on a network event when player manages to escape by mashing keys on their keyboard.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleTargetPlayerEscaped(string receivedAloeId)
    {
        if (!IsServer) return;
        
        LogVerbose("Target player escaped by force!");
        SetTargetPlayerInCaptivity(false);
        SwitchBehaviourState(AloeStates.ChasingEscapedPlayer);
    }
    
    /// <summary>
    /// Switches to the kidnapping state.
    /// This function is called by an animation event.
    /// </summary>
    public void GrabTargetPlayer()
    {
        if (!IsServer) return;
        if (CurrentState.GetStateType() is not AloeStates.AggressiveStalking) return;
        
        LogVerbose("Handling grab target player event.");
        SwitchBehaviourState(AloeStates.KidnappingPlayer);
    }
    
    /// <summary>
    /// Makes the Aloe look at the given position by rotating smoothly.
    /// </summary>
    /// <param name="position">The position to look at.</param>
    /// <param name="rotationSpeed">The speed at which to rotate at.</param>
    public void LookAtPosition(Vector3 position, float rotationSpeed = 30f)
    {
        Vector3 direction = (position - transform.position).normalized;
        direction.y = 0;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }
    
    /// <summary>
    /// Calculates the agents speed depending on whether the Aloe is stunned/dead/not dead
    /// </summary>
    private void CalculateSpeed()
    {
        if (stunNormalizedTimer > 0 || 
            IsStaringAtTargetPlayer ||
            InSlapAnimation || inCrushHeadAnimation ||
            (CurrentState.GetStateType() == AloeStates.AvoidingPlayer && !netcodeController.HasFinishedSpottedAnimation.Value) ||
            CurrentState.GetStateType() == AloeStates.Dead ||
            CurrentState.GetStateType() == AloeStates.Spawning)
        {
            agent.speed = 0;
            agent.acceleration = AgentMaxAcceleration;
        }
        else
        {
            MoveWithAcceleration();
        }
    }

    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, AgentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, AgentMaxAcceleration, accelerationAdjustment);
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        ActualTargetPlayer.Value = newValue == NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        targetPlayer = ActualTargetPlayer.Value;
        LogVerbose(ActualTargetPlayer.IsNotNull
            ? $"Changed target player to {ActualTargetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;
        
        netcodeController.OnTargetPlayerEscaped += HandleTargetPlayerEscaped;
        
        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;

        _networkEventsSubscribed = true;
    }

    /// <summary>
    /// Unsubscribe to the network events.
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !_networkEventsSubscribed) return;
        
        netcodeController.OnTargetPlayerEscaped -= HandleTargetPlayerEscaped;
        
        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;

        _networkEventsSubscribed = false;
    }

    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    internal void InitializeConfigValues()
    {
        if (!IsServer) return;

        // todo: add the new configs here
        // Maybe theres a modular way to do this? Doing it manually seems really dumb
        
        enemyHP = AloeHandler.Instance.Config.Health;
        PlayerHealthThresholdForStalking = AloeHandler.Instance.Config.PlayerHealthThresholdForStalking;
        PlayerHealthThresholdForHealing = AloeHandler.Instance.Config.PlayerHealthThresholdForHealing;
        TimeItTakesToFullyHealPlayer = AloeHandler.Instance.Config.TimeItTakesToFullyHealPlayer;
        PassiveStalkStaredownDistance = AloeHandler.Instance.Config.PassiveStalkStaredownDistance;
        WaitBeforeChasingEscapedPlayerTime = AloeHandler.Instance.Config.WaitBeforeChasingEscapedPlayerTime;

        roamMap.searchWidth = AloeHandler.Instance.Config.RoamingRadius;
        
        agent.angularSpeed = AloeHandler.Instance.Config.AngularSpeed;
        agent.autoBraking = AloeHandler.Instance.Config.AutoBraking;
        agent.height = AloeHandler.Instance.Config.NavMeshAgentHeight;
        agent.radius = AloeHandler.Instance.Config.NavMeshAgentRadius;
        agent.avoidancePriority = AloeHandler.Instance.Config.NavMeshAgentAvoidancePriority;
        
        AIIntervalTime = AloeHandler.Instance.Config.AiIntervalTime;
        
        netcodeController.InitializeConfigValuesClientRpc(BioId);
    }
}