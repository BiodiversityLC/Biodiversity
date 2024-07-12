using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.BehaviourStates;
using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.General;
using GameNetcodeStuff;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeServer : BiodiverseAI
{
    public ManualLogSource Mls;
    public string aloeId;
    
#if !UNITY_EDITOR
    [field: HideInInspector] [field: SerializeField] public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;
#endif
    
    [Header("AI and Pathfinding")] [Space(5f)]
    public AISearchRoutine roamMap;
    
    // The serialize field variables are pretty much options that can be edited with configs
    [SerializeField] private Vector2 amountRangeOfNodesToPathToInSearchRoutine = new(3, 8);
    [SerializeField] private float maxRoamingRadius = 50f;
    [SerializeField] private int proximityAwareness = 3;
    [SerializeField] private int playerHealthThresholdForStalking = 90;
    [SerializeField] private int playerHealthThresholdForHealing = 45;
    public float ViewWidth { get; private set; } = 115f;
    public int ViewRange { get; private set; } = 80;
    public float PassiveStalkStaredownDistance { get; private set; } = 10f;
    public float TimeItTakesToFullyHealPlayer { get; private set; } = 15f;
    public float WaitBeforeChasingEscapedPlayerTime { get; private set; } = 2f;
    
#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)] 
    public AloeNetcodeController netcodeController;
#pragma warning restore 0649

    [HideInInspector] public BehaviourState PreviousState;
    private Dictionary<States, BehaviourState> _stateDictionary = [];
    private BehaviourState _currentState;

    [HideInInspector] public Vector3 favouriteSpot;
    private Vector3 _mainEntrancePosition;
    private Vector3 _agentLastPosition;

    [HideInInspector] public float agentMaxAcceleration;
    [HideInInspector] public float agentMaxSpeed;
    private float _agentCurrentSpeed;
    private float _takeDamageCooldown;

    [HideInInspector] public int timesFoundSneaking;

    [HideInInspector] public bool reachedFavouriteSpotForRoaming;
    [HideInInspector] public bool inGrabAnimation;
    [HideInInspector] public bool hasTransitionedToRunningForwardsAndCarryingPlayer;
    [HideInInspector] public bool overridePlaySpottedAnimation;
    [HideInInspector] public bool isStaringAtTargetPlayer;
    private bool _networkEventsSubscribed;
    private bool _inStunAnimation;
    
    [HideInInspector] public readonly NullableObject<PlayerControllerB> ActualTargetPlayer = new();
    [HideInInspector] public readonly NullableObject<PlayerControllerB> AvoidingPlayer = new();
    [HideInInspector] public PlayerControllerB backupTargetPlayer;
    
    private static readonly Dictionary<Type, States> StateTypeMapping = new()
    {
        { typeof(SpawningState), States.Spawning },
        { typeof(PassiveRoamingState), States.PassiveRoaming },
        { typeof(AvoidingPlayerState), States.AvoidingPlayer },
        { typeof(PassivelyStalkingPlayerState), States.PassivelyStalkingPlayer },
        { typeof(StalkingPlayerToKidnapState), States.StalkingPlayerToKidnap },
        { typeof(KidnappingPlayerState), States.KidnappingPlayer },
        { typeof(HealingPlayerState), States.HealingPlayer },
        { typeof(CuddlingPlayerState), States.CuddlingPlayer },
        { typeof(ChasingEscapedPlayerState), States.ChasingEscapedPlayer },
        { typeof(AttackingPlayerState), States.AttackingPlayer },
        //{ typeof(DeadState), States.Dead },
    };

    public enum States
    {
        Spawning,
        PassiveRoaming,
        AvoidingPlayer,
        PassivelyStalkingPlayer,
        StalkingPlayerToKidnap,
        KidnappingPlayer,
        HealingPlayer,
        CuddlingPlayer,
        ChasingEscapedPlayer, 
        AttackingPlayer,
        Dead,
        Invalid,
    }
    
    private void Awake()
    {
        aloeId = Guid.NewGuid().ToString();
        Mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Server {aloeId}");
        
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();
        if (netcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, aborting spawn");
            Destroy(gameObject);
        }
    }
    
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }
    
    private void OnDisable()
    {
        #if DEBUG
        if (ActualTargetPlayer.IsNotNull) ActualTargetPlayer.Value.inSpecialInteractAnimation = false;
        #endif
        
        if (!IsServer) AloeSharedData.Instance.UnOccupyBrackenRoomAloeNode(favouriteSpot);
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        // Ensure SubscribeToNetworkEvents is called again in Start to handle network initialization timing
        SubscribeToNetworkEvents();

        Random.InitState(StartOfRound.Instance.randomMapSeed + aloeId.GetHashCode());
        netcodeController.SyncAloeIdClientRpc(aloeId);
        agent.updateRotation = false;

        ConstructStateDictionary();
        
        _currentState = _stateDictionary[States.Spawning];
        _currentState.OnStateEnter();
        
        LogDebug("Aloe Spawned!");
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
        else if (_inStunAnimation)
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
        if (stunNormalizedTimer > 0.0f) return;
        
        _currentState?.AIIntervalBehaviour();
        
        // Check for transitions
        foreach (StateTransition transition in (_currentState?.Transitions ?? []).Where(transition => transition.ShouldTransitionBeTaken()))
        {
            transition.OnTransition();
            SwitchBehaviourState(transition.NextState());
            break;
        }
    }

    private void LateUpdate()
    {
        if (!IsServer) return;
        if (isEnemyDead || StartOfRound.Instance.livingPlayers == 0) return;
        if (stunNormalizedTimer > 0.0f) return;
        
        _currentState?.LateUpdateBehaviour();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="newState"></param>
    /// <param name="stateTransition">Used to call an OnTransition() function in the state class</param>
    public void SwitchBehaviourState(States newState, StateTransition stateTransition = null)
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

        if (_stateDictionary.TryGetValue(newState, out BehaviourState newStateInstance))
        {
            _currentState = newStateInstance;
            currentBehaviourStateIndex = (int)newState;
            LogDebug($"Entering state {newState}.");
            _currentState.OnStateEnter();
            netcodeController.CurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
            
            LogDebug($"Successfully switched behaviour state to {newState}.");
        }
        else
        {
            Mls.LogError($"State {newState} not found in StateDictionary.");
        }
    }
    
    private void ConstructStateDictionary()
    {
        if (!IsServer) return;
        _stateDictionary = new Dictionary<States, BehaviourState>();

        foreach ((Type stateType, States stateEnum) in StateTypeMapping)
        {
            try
            {
                // Use reflection to find the constructor that takes AloeServer and States as parameters
                ConstructorInfo constructor = stateType.GetConstructor([typeof(AloeServer), typeof(States)]);
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
                AloeUtils.GetFarthestValidNodeFromPosition(
                        out AloeUtils.PathStatus pathStatus,
                        agent,
                        _mainEntrancePosition != Vector3.zero ? _mainEntrancePosition : transform.position,
                        allAINodes,
                        bufferDistance: 0f,
                        logSource: Mls)
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

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Dead or (int)States.HealingPlayer:
                break;
            
            case (int)States.CuddlingPlayer:
            {
                PlayerControllerB whoIsLookingAtAloe = AloeUtils.GetClosestPlayerLookingAtPosition(eye.transform, ActualTargetPlayer.Value, logSource: Mls);
                if (whoIsLookingAtAloe != null)
                {
                    LookAtPosition(whoIsLookingAtAloe.transform.position);
                }

                break;
            }
            
            default:
            {
                if (!(agent.velocity.sqrMagnitude > 0.01f)) break;
                Vector3 targetDirection = !hasTransitionedToRunningForwardsAndCarryingPlayer &&
                                          currentBehaviourStateIndex == (int)States.KidnappingPlayer
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
        netcodeController.TransitionToRunningForwardsAndCarryingPlayerClientRpc(aloeId);
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
        hasTransitionedToRunningForwardsAndCarryingPlayer = true;
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
        if (currentBehaviourStateIndex is (int)States.Dead) return;
        if (_takeDamageCooldown > 0) return;

        netcodeController.PlayAudioClipTypeServerRpc(aloeId, AloeClient.AudioClipTypes.Hit);
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        if (enemyHP > 0)
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)States.Spawning or (int)States.PassiveRoaming or (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
                {
                    if (playerWhoHit != null) AvoidingPlayer.Value = playerWhoHit;

                    overridePlaySpottedAnimation = true;
                    SwitchBehaviourState(States.AvoidingPlayer);
                    break; 
                }
                
                case (int)States.KidnappingPlayer or (int)States.HealingPlayer or (int)States.CuddlingPlayer:
                {
                    SetTargetPlayerInCaptivity(false);
                    if (playerWhoHit != null)
                    {
                        backupTargetPlayer = ActualTargetPlayer.Value;
                        netcodeController.TargetPlayerClientId.Value = playerWhoHit.actualClientId;
                        SwitchBehaviourState(States.AttackingPlayer);
                    }
                    else
                    {
                        overridePlaySpottedAnimation = true;
                        SwitchBehaviourState(States.AvoidingPlayer);
                    }
                
                    break;
                }
                
                case (int)States.AttackingPlayer:
                {
                    if (playerWhoHit == null) break;

                    if (ActualTargetPlayer.Value != playerWhoHit)
                        netcodeController.TargetPlayerClientId.Value = playerWhoHit.actualClientId;
                
                    break;
                }
            }
        }
        else
        {
            netcodeController.EnterDeathStateClientRpc(aloeId);
            KillEnemyServerRpc(false);
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
        if (!IsServer) return;

        _inStunAnimation = true;
        netcodeController.PlayAudioClipTypeServerRpc(aloeId, AloeClient.AudioClipTypes.Stun, true);
        netcodeController.AnimationParamStunned.Value = true;
        netcodeController.AnimationParamHealing.Value = false;
        netcodeController.ChangeLookAimConstraintWeightClientRpc(aloeId, 0, 0f);
        
        switch (currentBehaviourStateIndex)
        { 
            case (int)States.Spawning or (int)States.PassiveRoaming or (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
            {
                if (setStunnedByPlayer != null) AvoidingPlayer.Value = setStunnedByPlayer;

                overridePlaySpottedAnimation = true;
                SwitchBehaviourState(States.AvoidingPlayer);
                break; 
            }
            
            case (int)States.KidnappingPlayer or (int)States.HealingPlayer or (int)States.CuddlingPlayer:
            {
                SetTargetPlayerInCaptivity(false);
                if (setStunnedByPlayer != null)
                {
                    backupTargetPlayer = ActualTargetPlayer.Value;
                    netcodeController.TargetPlayerClientId.Value = setStunnedByPlayer.actualClientId;
                    SwitchBehaviourState(States.AttackingPlayer);
                }
                else
                {
                    overridePlaySpottedAnimation = true;
                    AvoidingPlayer.Value = null;
                    SwitchBehaviourState(States.AvoidingPlayer);
                }
                
                break;
            }
            
            case (int)States.AttackingPlayer:
            {
                if (setStunnedByPlayer == null) break;

                if (ActualTargetPlayer.Value != setStunnedByPlayer)
                    netcodeController.TargetPlayerClientId.Value = setStunnedByPlayer.actualClientId;
                
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
            if (!AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(this))
                AloeSharedData.Instance.AloeBoundKidnaps.Add(this, ActualTargetPlayer.Value);
        }
        else {
            if (AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(this))
                AloeSharedData.Instance.AloeBoundKidnaps.Remove(this);
        }
        
        netcodeController.SetTargetPlayerInCaptivityClientRpc(aloeId, setToInCaptivity);
    }

    /// <summary>
    /// Is called by the teleporter patch to make sure the aloe reacts appropriately when a player is teleported away
    /// </summary>
    public void SetTargetPlayerEscapedByTeleportation()
    {
        if (!IsServer) return;
        States localCurrentState = _currentState.GetStateType();
        if (localCurrentState is not (States.KidnappingPlayer or States.CuddlingPlayer or States.HealingPlayer)) return;
        
        LogDebug("Target player escaped by teleportation!");
        SetTargetPlayerInCaptivity(false);
        netcodeController.TargetPlayerClientId.Value = 69420;
        SwitchBehaviourState(States.PassiveRoaming);
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
        SwitchBehaviourState(States.ChasingEscapedPlayer);
    }

    /// <summary>
    /// Determines whether the given player is "stalkable" or not.
    /// </summary>
    /// <param name="player">The player to check if stalkable.</param>
    /// <returns>Whether the given player is stalkable</returns>
    public bool PlayerIsStalkable(PlayerControllerB player)
    {
        int healthThreshold = _currentState.GetStateType() == States.PassiveRoaming
            ? playerHealthThresholdForStalking
            : playerHealthThresholdForHealing;
        
        return player.health <= healthThreshold && AloeUtils.IsPlayerTargetable(player);
    }

    /// <summary>
    /// Makes the Aloe look at the given position by rotating smoothly.
    /// </summary>
    /// <param name="position">The position to look at.</param>
    /// <param name="rotationSpeed">The speed at which to rotate at.</param>
    public void LookAtPosition(Vector3 position, float rotationSpeed = 30f)
    {
        Vector3 direction = (position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    /// <summary>
    /// Switches to the kidnapping state.
    /// This function is called by a network event which is called by an animation event.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleGrabTargetPlayer(string receivedAloeId)
    {
        if (!IsServer) return;
        if (aloeId != receivedAloeId) return;
        if (currentBehaviourStateIndex != (int)States.StalkingPlayerToKidnap) return;
        
        LogDebug("Grab target player network event triggered");
        SwitchBehaviourState(States.KidnappingPlayer);
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
            (_currentState.GetStateType() == States.AvoidingPlayer && !netcodeController.HasFinishedSpottedAnimation.Value) ||
            _currentState.GetStateType() == States.Dead ||
            _currentState.GetStateType() == States.Spawning
           // || currentBehaviourStateIndex == (int)States.KidnappingPlayer
            )
        {
            agent.speed = 0;
            agent.acceleration = agentMaxAcceleration;
        }
        else if (inGrabAnimation)
        {
            agent.speed = 1;
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
        ActualTargetPlayer.Value = newValue == 69420 ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        targetPlayer = ActualTargetPlayer.Value;
        LogDebug(ActualTargetPlayer.IsNotNull
            ? $"Changed target player to {ActualTargetPlayer.Value?.playerUsername},"
            : "Changed target player to null,");
    }

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;
        
        netcodeController.OnTargetPlayerEscaped += HandleTargetPlayerEscaped;
        netcodeController.OnGrabTargetPlayer += HandleGrabTargetPlayer;

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
        netcodeController.OnGrabTargetPlayer -= HandleGrabTargetPlayer;
        
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

        maxRoamingRadius = Config.MaxRoamingRadius;
        ViewWidth = Config.ViewWidth;
        ViewRange = Config.ViewRange;
        playerHealthThresholdForStalking = Config.PlayerHealthThresholdForStalking;
        playerHealthThresholdForHealing = Config.PlayerHealthThresholdForHealing;
        TimeItTakesToFullyHealPlayer = Config.TimeItTakesToFullyHealPlayer;
        PassiveStalkStaredownDistance = Config.PassiveStalkStaredownDistance;
        WaitBeforeChasingEscapedPlayerTime = Config.WaitBeforeChasingEscapedPlayerTime;
        
        roamMap.searchWidth = maxRoamingRadius;
        
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