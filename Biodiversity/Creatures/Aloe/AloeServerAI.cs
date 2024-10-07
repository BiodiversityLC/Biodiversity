using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.BehaviourStates;
using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

#pragma warning disable CS0169 // Field is never used

namespace Biodiversity.Creatures.Aloe;

internal class AloeServerAI : StateManagedAI<AloeServerAI.AloeStates, AloeServerAI>
{
    public AISearchRoutine roamMap;
    
    public int PlayerHealthThresholdForStalking { get; private set; } = 90;
    public int PlayerHealthThresholdForHealing { get; private set; } = 45;
    public float ViewWidth { get; private set; } = 115f;
    public int ViewRange { get; private set; } = 80;
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

    public readonly NullableObject<PlayerControllerB> ActualTargetPlayer = new();
    public readonly NullableObject<PlayerControllerB> AvoidingPlayer = new();
    public readonly NullableObject<PlayerControllerB> SlappingPlayer = new();
    [HideInInspector] public PlayerControllerB backupTargetPlayer;
    
    [HideInInspector] public Vector3 favouriteSpot;
    private Vector3 _mainEntrancePosition;
    private Vector3 _agentLastPosition;
    
    [HideInInspector] public float agentMaxAcceleration;
    [HideInInspector] public float agentMaxSpeed;
    [SerializeField] private float roamingRadius = 50f;
    [SerializeField] private float lookAheadDistance = 200f;
    private float _agentCurrentSpeed;
    private float _takeDamageCooldown;

    [HideInInspector] public int timesFoundSneaking;

    public const ulong NullPlayerId = 69420;

    [HideInInspector] public bool hasTransitionedToRunningForwardsAndCarryingPlayer;
    [HideInInspector] public bool isStaringAtTargetPlayer;
    [HideInInspector] public bool inGrabAnimation;
    [HideInInspector] public bool inSlapAnimation;
    [HideInInspector] public bool inCrushHeadAnimation;
    private bool _networkEventsSubscribed;
    private bool _inStunAnimation;

    protected override void Awake()
    {
        base.Awake();
        
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();
        if (netcodeController == null)
        {
            LogError("Netcode Controller is null, aborting spawn");
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (!IsServer) return;
        SubscribeToNetworkEvents();
    }
    
    private void OnDisable()
    {
        #if DEBUG
        if (ActualTargetPlayer.IsNotNull) ActualTargetPlayer.Value.inSpecialInteractAnimation = false;
        #endif

        if (!IsServer) return;
        
        AloeSharedData.Instance.UnOccupyBrackenRoomAloeNode(favouriteSpot);
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        SubscribeToNetworkEvents();
        
        netcodeController.SyncAloeIdClientRpc(BioId);
        agent.updateRotation = false;
        
        LogVerbose("Aloe Spawned!");
    }

    /// <summary>
    /// This function is called every frame
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        if (isEnemyDead || StartOfRound.Instance.livingPlayers == 0) return;

        _takeDamageCooldown -= Time.deltaTime;
        
        CalculateAgentSpeed();
        CalculateRotation();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation)
        {
            netcodeController.AnimationParamStunned.Value = false;
            _inStunAnimation = false;
        }
        else if (_inStunAnimation || inSlapAnimation || inCrushHeadAnimation)
        {
            return;
        }
        
        _currentState?.UpdateBehaviour();
    }
    
    /// <summary>
    /// Handles most of the main AI logic
    /// The logic in this method is not run every frame
    /// </summary>
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;
        if (isEnemyDead || StartOfRound.Instance.livingPlayers == 0) return;
        if (_inStunAnimation || inSlapAnimation) return;
        
        _currentState?.AIIntervalBehaviour();
        
        // Check for transitions
        foreach (StateTransition transition in (_currentState?.Transitions ?? []).Where(transition => transition.ShouldTransitionBeTaken()))
        {
            transition.OnTransition();
            LogDebug("SwitchBehaviourState() called from DoAIInterval()");
            SwitchBehaviourState(transition.NextState());
            break;
        }
    }

    private void LateUpdate()
    {
        if (!IsServer) return;
        if (isEnemyDead || StartOfRound.Instance.livingPlayers == 0) return;
        if (_inStunAnimation || inSlapAnimation) return;
        
        _currentState?.LateUpdateBehaviour();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newAloeState"></param>
    /// <param name="stateTransition">Used to call an OnTransition() function in the state class.</param>
    /// <param name="initData">Used to provide optional additional data to the new state.</param>
    public void SwitchBehaviourState(
        AloeStates newAloeState, 
        StateTransition stateTransition = null,
        StateData initData = null)
    {
        if (!IsServer) return;
        if (_currentState != null)
        {
            LogDebug($"Exiting state {_currentState.GetType().Name}.");
            _currentState.OnStateExit();
            PreviousState = _currentState;
            previousBehaviourStateIndex = currentBehaviourStateIndex;

            stateTransition?.OnTransition();
        }
        else
        {
            LogDebug("Could not exit current state, because it is null.");
        }

        if (_stateDictionary.TryGetValue(newAloeState, out BehaviourState newStateInstance))
        {
            _currentState = newStateInstance;
            currentBehaviourStateIndex = (int)newAloeState;
            LogDebug($"Entering state {newAloeState}.");
            
            _currentState.OnStateEnter(ref initData);
            
            netcodeController.CurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
            
            LogDebug($"Successfully switched behaviour state to {newAloeState}.");
        }
        else
        {
            Mls.LogError($"State {newAloeState} not found in StateDictionary.");
        }
    }
    
    private void ConstructStateDictionary()
    {
        if (!IsServer) return;
        _stateDictionary = new Dictionary<AloeStates, BehaviourState>();

        foreach ((Type stateType, AloeStates stateEnum) in StateTypeMapping)
        {
            try
            {
                // Use reflection to find the constructor that takes AloeServerInstance and States as parameters
                ConstructorInfo constructor = stateType.GetConstructor([typeof(AloeServerAI), typeof(AloeStates)]);
                if (constructor != null)
                {
                    // Create an instance of the state type using the constructor
                    BehaviourState stateInstance = (BehaviourState)constructor.Invoke([this, stateEnum]);
                    _stateDictionary[stateEnum] = stateInstance;
                    LogDebug($"Successfully created instance of {stateType.Name} for state {stateEnum}");
                }
                else
                {
                    Mls.LogError($"Constructor not found for type {stateType.Name}");
                }
            }
            catch (Exception ex)
            {
                Mls.LogError($"Error creating instance of {stateType.Name}: {ex.Message}");
            }
        }
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
                agent.Warp(RoundManager.Instance.GetRandomNavMeshPositionInRadius(_mainEntrancePosition, 30f));
            }
        }

        //_favouriteSpot = brackenRoomAloeNode;
        favouriteSpot = Vector3.zero;
        if (favouriteSpot == Vector3.zero)
        {
            favouriteSpot =
                GetFarthestValidNodeFromPosition(
                        out PathStatus pathStatus,
                        agent,
                        _mainEntrancePosition != Vector3.zero ? _mainEntrancePosition : transform.position,
                        allAINodes,
                        bufferDistance: 0f)
                    .position;
        }
        
        LogDebug($"Found a favourite spot: {favouriteSpot}");
    }
    
    /// <summary>
    /// Calculates the rotation for the Aloe manually, which is needed because of the kidnapping animation
    /// </summary>
    private void CalculateRotation()
    {
        const float turnSpeed = 5f;

        if (inCrushHeadAnimation) return;
        if (inSlapAnimation && SlappingPlayer.IsNotNull)
        {
            LookAtPosition(SlappingPlayer.Value.transform.position, 30f);
        }

        switch (_currentState.GetStateType())
        {
            case AloeStates.Dead or AloeStates.HealingPlayer or AloeStates.CuddlingPlayer:
                break;
            
            default:
            {
                if (!(agent.velocity.sqrMagnitude > 0.01f)) break;
                Vector3 targetDirection = !hasTransitionedToRunningForwardsAndCarryingPlayer &&
                                          currentBehaviourStateIndex == (int)AloeStates.KidnappingPlayer
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
        netcodeController.TransitionToRunningForwardsAndCarryingPlayerClientRpc(aloeId, transitionDuration);
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            yield return null;
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
        }

        transform.rotation = targetRotation;
        hasTransitionedToRunningForwardsAndCarryingPlayer = true;
        agentMaxAcceleration = 20f;
        agentMaxSpeed = 10f;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsServer) return;
        switch (_currentState.GetStateType())
        {
            case AloeStates.ChasingEscapedPlayer:
            {
                if (((ChasingEscapedPlayerState)_currentState).WaitBeforeChasingTimer > 0) break;
                
                LogDebug("Player is touching the aloe! Kidnapping him now.");
                netcodeController.SetAnimationTriggerClientRpc(aloeId, AloeClient.Grab);
                SwitchBehaviourState(AloeStates.KidnappingPlayer);
                break;
            }

            case AloeStates.AttackingPlayer:
            {
                LogDebug("Player is touching the aloe! Killing them!");
                netcodeController.CrushPlayerClientRpc(aloeId, ActualTargetPlayer.Value.actualClientId);
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
        
        AloeStates currentAloeStateType = _currentState.GetStateType();
        if (currentAloeStateType is AloeStates.Dead || _takeDamageCooldown > 0) return;

        netcodeController.PlayAudioClipTypeServerRpc(aloeId, AloeClient.AudioClipTypes.Hit);
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
                            LogDebug("Triggering bitch slap.");
                            SlappingPlayer.Value = playerWhoHitMe.Value;
                            netcodeController.SetAnimationTriggerClientRpc(aloeId, AloeClient.Slap);
                        }
                        else 
                            LogDebug($"Did not trigger bitch slap. Current health: {enemyHP}. Health needed to trigger slap: {AloeHandler.Instance.Config.Health / 2}");
                        
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
                        backupTargetPlayer = ActualTargetPlayer.Value;
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
                        backupTargetPlayer = ActualTargetPlayer.Value;
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
        
        AloeStates currentAloeState = _currentState.GetStateType();
        if (currentAloeState is AloeStates.Dead) return;

        _inStunAnimation = true;
        netcodeController.PlayAudioClipTypeServerRpc(aloeId, AloeClient.AudioClipTypes.Stun, true);
        netcodeController.AnimationParamStunned.Value = true;
        netcodeController.AnimationParamHealing.Value = false;
        netcodeController.ChangeLookAimConstraintWeightClientRpc(aloeId, 0, 0f);

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
                    backupTargetPlayer = ActualTargetPlayer.Value;
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
        
        netcodeController.SetTargetPlayerInCaptivityClientRpc(aloeId, setToInCaptivity);
    }

    /// <summary>
    /// Is called by the teleporter patch to make sure the aloe reacts appropriately when a player is teleported away
    /// </summary>
    public void SetTargetPlayerEscapedByTeleportation()
    {
        if (!IsServer) return;
        if (!ActualTargetPlayer.IsNotNull)
        {
            Mls.LogWarning($"{nameof(SetTargetPlayerEscapedByTeleportation)} called, but the target player object is null.");
            return;
        }
        
        AloeStates localCurrentAloeState = _currentState.GetStateType();
        if (localCurrentAloeState is not (AloeStates.KidnappingPlayer or AloeStates.CuddlingPlayer or AloeStates.HealingPlayer)) return;
        
        LogDebug("Target player escaped by teleportation!");
        if (AloeSharedData.Instance.IsPlayerStalkBound(ActualTargetPlayer.Value))
            AloeSharedData.Instance.Unbind(this, BindType.Stalk);
        SetTargetPlayerInCaptivity(false);
            
        netcodeController.TargetPlayerClientId.Value = NullPlayerId;
        SwitchBehaviourState(AloeStates.Roaming);
    }

    /// <summary>
    /// Is called on a network event when player manages to escape by mashing keys on their keyboard.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleTargetPlayerEscaped(string receivedAloeId)
    {
        if (!IsServer) return;
        
        LogDebug("Target player escaped by force!");
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
        if (currentBehaviourStateIndex != (int)AloeStates.AggressiveStalking) return;
        
        LogDebug("Handling grab target player event.");
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
    /// Calculates the agents speed depending on whether the aloe is stunned/dead/not dead
    /// </summary>
    private void CalculateAgentSpeed()
    {
        if (!IsServer) return;
        
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        
        if (stunNormalizedTimer > 0 || 
            isStaringAtTargetPlayer ||
            inSlapAnimation || inCrushHeadAnimation ||
            (_currentState.GetStateType() == AloeStates.AvoidingPlayer && !netcodeController.HasFinishedSpottedAnimation.Value) ||
            _currentState.GetStateType() == AloeStates.Dead ||
            _currentState.GetStateType() == AloeStates.Spawning)
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
        }
        else
        {
            MoveWithAcceleration();
        }
    }

    /// <summary>
    /// Makes the agent move by using interpolation to make the movement smooth
    /// </summary>
    private void MoveWithAcceleration()
    {
        if (!IsServer) return;
        
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, agentMaxAcceleration, accelerationAdjustment);
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        ActualTargetPlayer.Value = newValue == NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        targetPlayer = ActualTargetPlayer.Value;
        LogDebug(ActualTargetPlayer.IsNotNull
            ? $"Changed target player to {ActualTargetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    public Vector3 GetLookAheadVector()
    {
        return rootTransform.forward * lookAheadDistance + headBone.up;
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
    public void InitializeConfigValues()
    {
        if (!IsServer) return;

        enemyHP = AloeHandler.Instance.Config.Health;
        roamingRadius = AloeHandler.Instance.Config.RoamingRadius;
        ViewWidth = 135f;
        ViewRange = 65;
        PlayerHealthThresholdForStalking = AloeHandler.Instance.Config.PlayerHealthThresholdForStalking;
        PlayerHealthThresholdForHealing = AloeHandler.Instance.Config.PlayerHealthThresholdForHealing;
        TimeItTakesToFullyHealPlayer = AloeHandler.Instance.Config.TimeItTakesToFullyHealPlayer;
        PassiveStalkStaredownDistance = AloeHandler.Instance.Config.PassiveStalkStaredownDistance;
        WaitBeforeChasingEscapedPlayerTime = AloeHandler.Instance.Config.WaitBeforeChasingEscapedPlayerTime;

        roamMap.searchWidth = roamingRadius;
        
        netcodeController.InitializeConfigValuesClientRpc(aloeId);
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log.</param>
    public void LogDebug(string msg)
    {
        #if DEBUG
        Mls?.LogInfo($"State:{currentBehaviourStateIndex}, {msg}");
        #endif
    }
}