using System.Collections;
using System.Collections.Generic;
using Biodiversity.Creatures.Aloe.BehaviourStates;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeServerAI : StateManagedAI<AloeServerAI.States, AloeServerAI>
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
    [Header("Controllers")] [Space(5f)] 
    public AloeNetcodeController netcodeController;
#pragma warning restore 0649
    
    public enum States
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

    internal CachedNullable<PlayerControllerB> ActualTargetPlayer;
    internal CachedNullable<PlayerControllerB> AvoidingPlayer;
    internal CachedNullable<PlayerControllerB> SlappingPlayer;
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
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            AloeSharedData.Instance.UnOccupyBrackenRoomAloeNode(FavouriteSpot);
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        SubscribeToNetworkEvents();
        
        agent.updateRotation = false;
        
        CollectAudioClipsAndSources();
        
        PlayerTargetableConditions.AddCondition(player => player.isInsideFactory);
        PlayerTargetableConditions.AddCondition(player => player.sinkingValue < 0.7300000190734863);
        PlayerTargetableConditions.AddCondition(player => !AloeSharedData.Instance.IsPlayerKidnapBound(player));
        
        LogVerbose("Aloe spawned!");
    }
    
    protected override States DetermineInitialState()
    {
        return States.Spawning;
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
    /// Then it populates the <see cref="AloeServerAI.AudioClips"/> and <see cref="AloeServerAI.AudioSources"/> dictionaries based on field names and values.
    /// </summary>
    private void CollectAudioClipsAndSources()
    {
        LogVerbose($"In {nameof(CollectAudioClipsAndSources)}");
        
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

    public void PickFavouriteSpot()
    {
        if (!IsServer) return;
        
        // todo: fix the aloe shared data thing
        // todo: add a lazer pointer type thing with the required features so ppl can easily go around a map and get the coordinates of a good spot for an aloe node &/or wax soldier guard post
        _mainEntrancePosition = RoundManager.FindMainEntrancePosition(true);
        Vector3 brackenRoomAloeNode = AloeSharedData.Instance.OccupyBrackenRoomAloeNode();

        // Make sure the Aloe has the correct AI nodes assigned
        Vector3 enemyPos = transform.position;
        Vector3 closestOutsideNode = Vector3.positiveInfinity;
        Vector3 closestInsideNode = Vector3.positiveInfinity;
        
        // todo: handle cases where these return a list of doodoo nodes (either just an empty list or a list of destroyed nodes).
        GameObject[] outsideAINodes = AloeSharedData.Instance.GetOutsideAINodes();
        GameObject[] insideAINodes = AloeSharedData.Instance.GetInsideAINodes();

        for (int i = 0; i < outsideAINodes.Length; i++)
        {
            GameObject node = outsideAINodes[i];
            Vector3 nodePos = node.transform.position;
            if ((nodePos - enemyPos).sqrMagnitude < (closestOutsideNode - enemyPos).sqrMagnitude)
                closestOutsideNode = nodePos;
        }

        for (int i = 0; i < insideAINodes.Length; i++)
        {
            GameObject node = insideAINodes[i];
            Vector3 nodePos = node.transform.position;
            if ((nodePos - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude)
                closestInsideNode = nodePos;
        }

        allAINodes = (closestOutsideNode - enemyPos).sqrMagnitude < (closestInsideNode - enemyPos).sqrMagnitude ? outsideAINodes : insideAINodes;

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
        if (InSlapAnimation && SlappingPlayer.HasValue)
        {
            LookAtPosition(SlappingPlayer.Value.transform.position);
        }

        States currentState = CurrentState.GetStateType();
        switch (currentState)
        {
            case States.Dead or States.HealingPlayer or States.CuddlingPlayer:
                break;
            
            default:
            {
                if (!(agent.velocity.sqrMagnitude > 0.01f)) break;
                Vector3 targetDirection = !HasTransitionedToRunningForwardsAndCarryingPlayer &&
                                          currentState == States.KidnappingPlayer
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
        netcodeController.TransitionToRunningForwardsAndCarryingPlayerClientRpc(transitionDuration);
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

    #region Lethal Company Vanilla Events

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        switch (CurrentState.GetStateType())
        {
            case States.ChasingEscapedPlayer:
            {
                if (((ChasingEscapedPlayerState)CurrentState).WaitBeforeChasingTimer > 0) break;
                
                LogVerbose("Player is touching the aloe! Kidnapping him now.");
                netcodeController.SetAnimationTriggerClientRpc(AloeClient.Grab);
                SwitchBehaviourState(States.KidnappingPlayer);
                break;
            }

            case States.AttackingPlayer:
            {
                LogVerbose("Player is touching the aloe! Killing them!");
                netcodeController.CrushPlayerClientRpc(ActualTargetPlayer.Value.actualClientId);
                SwitchBehaviourState(States.ChasingEscapedPlayer);
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
        if (!IsServer || isEnemyDead) return;
        
        States currentStateType = CurrentState.GetStateType();
        if (_takeDamageCooldown > 0 || currentStateType is States.Dead) return;

        PlayRandomAudioClipTypeServerRpc(nameof(AloeClient.AudioClipTypes.hitSfx), nameof(AloeClient.AudioSourceTypes.aloeVoiceSource), false, true, false, true);
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        
        if (enemyHP > 0)
        {
            if (currentStateType is States.Spawning) return;

            CachedNullable<PlayerControllerB> playerWhoHitMe = new(playerWhoHit);
            
            StateData stateData = new();
            stateData.Add("overridePlaySpottedAnimation", true);
            
            switch (currentStateType)
            {
                case States.Roaming or States.AvoidingPlayer or States.PassiveStalking or States.AggressiveStalking:
                {
                    if (playerWhoHitMe.HasValue)
                    {
                        if (currentStateType is not (States.Roaming or States.AvoidingPlayer) || enemyHP <= AloeHandler.Instance.Config.Health / 2)
                        {
                            LogVerbose("Triggering bitch slap.");
                            SlappingPlayer.Set(playerWhoHitMe.Value);
                            netcodeController.SetAnimationTriggerClientRpc(AloeClient.Slap);
                        }
                        else 
                            LogVerbose($"Did not trigger bitch slap. Current health: {enemyHP}. Health needed to trigger slap: {AloeHandler.Instance.Config.Health / 2}");
                        
                        AvoidingPlayer.Set(playerWhoHitMe.Value);
                        stateData.Add("hitByEnemy", false);
                    }
                    else
                    {
                        if (force <= 0) return;
                        stateData.Add("hitByEnemy", true);
                    }
                    
                    if (currentStateType is not States.AvoidingPlayer) SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                    
                    break;
                }
                
                case States.KidnappingPlayer or States.HealingPlayer or States.CuddlingPlayer:
                {
                    if (playerWhoHitMe.HasValue)
                    {
                        SetTargetPlayerInCaptivity(false);
                        BackupTargetPlayer = ActualTargetPlayer.Value;
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                        SwitchBehaviourState(States.AttackingPlayer);
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        SetTargetPlayerInCaptivity(false);
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                    }
                
                    break;
                }
                
                case States.ChasingEscapedPlayer:
                {
                    if (playerWhoHitMe.HasValue)
                    {
                        BackupTargetPlayer = ActualTargetPlayer.Value;
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                        SwitchBehaviourState(States.AttackingPlayer);
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                    }
                    
                    break;
                }
                
                case States.AttackingPlayer:
                {
                    if (playerWhoHitMe.HasValue && ActualTargetPlayer.Value != playerWhoHitMe.Value)
                    {
                        netcodeController.TargetPlayerClientId.Value = playerWhoHitMe.Value!.actualClientId;
                    }
                    else
                    {
                        if (force <= 0) return;
                        
                        stateData.Add("hitByEnemy", true);
                        SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                    }
                
                    break;
                }
            }
        }
        else
        {
            SwitchBehaviourState(States.Dead);
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
        if (!IsServer || isEnemyDead) return;
        
        States currentState = CurrentState.GetStateType();
        if (currentState is States.Dead) return;

        _inStunAnimation = true;
        PlayRandomAudioClipTypeServerRpc(nameof(AloeClient.AudioClipTypes.stunSfx), nameof(AloeClient.AudioSourceTypes.aloeVoiceSource), true, true, false, true);
        netcodeController.AnimationParamStunned.Value = true;
        netcodeController.AnimationParamHealing.Value = false;

        StateData stateData = new();
        stateData.Add("overridePlaySpottedAnimation", true);
        
        CachedNullable<PlayerControllerB> stunnedByPlayer2 = new(setStunnedByPlayer);
        switch (currentState)
        { 
            case States.Spawning or States.Roaming or States.PassiveStalking or States.AggressiveStalking:
            {
                if (stunnedByPlayer2.HasValue)
                    AvoidingPlayer.Set(stunnedByPlayer2.Value);
                
                SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                break;
            }
            
            case States.KidnappingPlayer or States.HealingPlayer or States.CuddlingPlayer:
            {
                SetTargetPlayerInCaptivity(false);
                if (stunnedByPlayer2.HasValue)
                {
                    BackupTargetPlayer = ActualTargetPlayer.Value;
                    netcodeController.TargetPlayerClientId.Value = stunnedByPlayer2.Value.actualClientId;
                    SwitchBehaviourState(States.AttackingPlayer);
                }
                else
                {
                    AvoidingPlayer.Reset();
                    SwitchBehaviourState(States.AvoidingPlayer, initData: stateData);
                }
                
                break;
            }
            
            case States.AttackingPlayer:
            {
                if (!stunnedByPlayer2.HasValue) break;

                if (ActualTargetPlayer.Value != setStunnedByPlayer)
                    netcodeController.TargetPlayerClientId.Value = stunnedByPlayer2.Value.actualClientId;
                
                break;
            }
        }
    }

    #endregion
    
    /// <summary>
    /// Creates a bind in the AloeBoundKidnaps dictionary and calls a network event to do several things in the client for kidnapping the target player.
    /// </summary>
    /// <param name="setToInCaptivity">Whether the target player is being kidnapped or finished being kidnapped.</param>
    public void SetTargetPlayerInCaptivity(bool setToInCaptivity)
    {
        if (!IsServer) return;
        if (!ActualTargetPlayer.HasValue) return;
        
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
        
        netcodeController.SetTargetPlayerInCaptivityClientRpc(setToInCaptivity);
    }

    /// <summary>
    /// Is called by the teleporter patch to make sure the aloe reacts appropriately when a player is teleported away
    /// </summary>
    public void SetTargetPlayerEscapedByTeleportation()
    {
        if (!IsServer) return;
        if (!ActualTargetPlayer.HasValue)
        {
            LogWarning($"{nameof(SetTargetPlayerEscapedByTeleportation)} called, but the target player object is null.");
            return;
        }
        
        States localCurrentState = CurrentState.GetStateType();
        if (localCurrentState is not (States.KidnappingPlayer or States.CuddlingPlayer or States.HealingPlayer)) return;
        
        LogVerbose("Target player escaped by teleportation!");
        if (AloeSharedData.Instance.IsPlayerStalkBound(ActualTargetPlayer.Value))
            AloeSharedData.Instance.Unbind(this, BindType.Stalk);
        SetTargetPlayerInCaptivity(false);
        
        netcodeController.TargetPlayerClientId.Value = NullPlayerId;
        SwitchBehaviourState(States.Roaming);
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
    private void HandleTargetPlayerEscaped()
    {
        if (!IsServer) return;
        
        LogVerbose("Target player escaped by force!");
        SetTargetPlayerInCaptivity(false);
        SwitchBehaviourState(States.ChasingEscapedPlayer);
    }
    
    /// <summary>
    /// Switches to the kidnapping state.
    /// This function is called by an animation event.
    /// </summary>
    public void GrabTargetPlayer()
    {
        if (!IsServer) return;
        if (CurrentState.GetStateType() is not States.AggressiveStalking) return;
        
        LogVerbose("Handling grab target player event.");
        SwitchBehaviourState(States.KidnappingPlayer);
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
    
    #region Animation State Callbacks

    public void OnSpawnAnimationStateExit()
    {
        LogVerbose("Spawn animation complete.");
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnSpawnAnimationStateExit));
    }
    
    public void OnSpottedAnimationStateEnter()
    {
        if (!IsServer) return;
        PlayRandomAudioClipTypeServerRpc(
            nameof(AloeClient.AudioClipTypes.interruptedHealingSfx),
            nameof(AloeClient.AudioSourceTypes.aloeVoiceSource),
            true, true, false, true);
    }
    
    public void OnSpottedAnimationStateExit()
    {
        LogVerbose("Spotted animation complete.");
        if (!IsServer) return;
        netcodeController.HasFinishedSpottedAnimation.Value = true;
    }
    
    public void OnDragPlayerAnimationStateEnter()
    {
        LogVerbose("Grab player animation complete.");
        if (!IsServer) return;

        if (!ActualTargetPlayer.HasValue ||
            PlayerUtil.IsPlayerDead(ActualTargetPlayer.Value) ||
            !ActualTargetPlayer.Value.isInsideFactory)
        {
            SwitchBehaviourState(States.Roaming);
        }
        else
        {
            GrabTargetPlayer(); 
        }
            
    }

    #endregion
    
    /// <summary>
    /// Calculates the agents speed depending on whether the Aloe is stunned/dead/not dead
    /// </summary>
    private void CalculateSpeed()
    {
        States currentState = CurrentState.GetStateType();
        
        if (stunNormalizedTimer > 0 || 
            IsStaringAtTargetPlayer ||
            InSlapAnimation || inCrushHeadAnimation ||
            (currentState == States.AvoidingPlayer && !netcodeController.HasFinishedSpottedAnimation.Value) ||
            currentState == States.Spawning)
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
        ActualTargetPlayer.Set(newValue == NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue]);
        targetPlayer = ActualTargetPlayer.Value;
        LogVerbose(ActualTargetPlayer.HasValue
            ? $"Changed target player to {ActualTargetPlayer.Value.playerUsername}."
            : "Changed target player to null.");
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
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr) || clipArr == null || clipArr.Length == 0)
        {
            LogWarning($"Audio Clip Type '{audioClipType}' not found, is null, or empty.");
            return;
        }

        // Validate audio source type
        // todo: is there a way around doing this null check every time?
        // maybe wrap the audio sources in the dictionary with a cached nullable? 
        if (!AudioSources.TryGetValue(audioSourceType, out AudioSource source) || source == null)
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
        if (clipToPlay == null)
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
        agent.avoidancePriority = AloeHandler.Instance.Config.NavMeshAgentAvoidancePriority;
        
        AIIntervalTime = AloeHandler.Instance.Config.AiIntervalTime;
        openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        netcodeController.InitializeConfigValuesClientRpc();
    }
}