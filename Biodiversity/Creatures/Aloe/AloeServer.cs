using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Biodiversity.General;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeServer : BiodiverseAI
{
    private ManualLogSource _mls;
    private string _aloeId;
    
#if !UNITY_EDITOR
    [field: HideInInspector] [field: SerializeField] public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;
#endif
    
    [Header("AI and Pathfinding")] [Space(5f)]
    public AISearchRoutine roamMap;
    
    // The serialize field variables are pretty much options that can be edited with configs
    [SerializeField] private float maxRoamingRadius = 50f;
    [SerializeField] private float viewWidth = 135f;
    [SerializeField] private int viewRange = 80;
    [SerializeField] private int proximityAwareness = 3;
    [SerializeField] private int playerHealthThresholdForStalking = 90;
    [SerializeField] private int playerHealthThresholdForHealing = 45;
    [SerializeField] private float passiveStalkStaredownDistance = 10f;
    [SerializeField] private float timeItTakesToFullyHealPlayer = 15f;
    
#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private AloeNetcodeController netcodeController;
#pragma warning restore 0649

    private Vector3 _mainEntrancePosition;
    private Vector3 _agentLastPosition;
    private Vector3 _favouriteSpot;
    
    private float _agentMaxAcceleration;
    private float _agentMaxSpeed;
    private float _agentCurrentSpeed;
    private float _takeDamageCooldown;
    private float _avoidPlayerTimer;
    private float _waitTimer;
    private float _avoidPlayerAudioTimer;
    private float _dragPlayerTimer;

    private int _timesFoundSneaking;
    private int _healingPerInterval;

    private bool _networkEventsSubscribed;
    private bool _isStaringAtTargetPlayer;
    private bool _currentlyHasDarkSkin;
    private bool _reachedFavouriteSpotForRoaming;
    private bool _inGrabAnimation;
    private bool _inStunAnimation;
    private bool _finishedSpottedAnimation;
    private bool _hasTransitionedToRunningForwardsAndCarryingPlayer;
    
    private readonly List<Transform> _ignoredNodes = [];

    private PlayerControllerB _avoidingPlayer;
    private PlayerControllerB _backupTargetPlayer;

    private Coroutine _avoidPlayerCoroutine;

    public enum States
    {
        Spawning,
        PassiveRoaming,
        AvoidingPlayer,
        PassivelyStalkingPlayer,
        StalkingPlayerToKidnap,
        GrabbingPlayer,
        KidnappingPlayer,
        HealingPlayer,
        CuddlingPlayer,
        ChasingEscapedPlayer,
        AttackingPlayer,
        Dead
    }
    
    private void Awake()
    {
        _aloeId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Server {_aloeId}");
        
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();
        if (netcodeController == null)
        {
            _mls.LogError("Netcode Controller is null, aborting spawn");
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
        if (targetPlayer != null) targetPlayer.inSpecialInteractAnimation = false;
        #endif
        
        if (!IsServer) AloeSharedData.Instance.UnOccupyBrackenRoomAloeNode(_favouriteSpot);
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        // Ensure SubscribeToNetworkEvents is called again in Start to handle network initialization timing
        SubscribeToNetworkEvents();
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + _aloeId.GetHashCode());
        netcodeController.SyncAloeIdClientRpc(_aloeId);
        agent.updateRotation = false;
        
        InitializeConfigValues();
        PickFavouriteSpot();
        
        LogDebug("Aloe Spawned!");
    }

    /// <summary>
    /// This function is called every frame
    /// </summary>
    public override void Update()
    {
        base.Update();
        if (!IsServer) return;
        if (isEnemyDead) return;

        _takeDamageCooldown -= Time.deltaTime;
        
        CalculateAgentSpeed();
        CalculateRotation();
        
        if (stunNormalizedTimer <= 0.0 && _inStunAnimation)
        {
            netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Stunned, false);
            _inStunAnimation = false;
        }
        else if (_inStunAnimation)
        {
            return;
        }

        switch (currentBehaviourStateIndex)
        {
            case (int)States.AvoidingPlayer:
            {
                _avoidPlayerAudioTimer -= Time.deltaTime;

                // Make the Aloe stay still until the spotted animation is finished
                if (!_finishedSpottedAnimation)
                {
                    if (_avoidingPlayer != null) LookAtPosition(_avoidingPlayer.transform.position);
                    
                    moveTowardsDestination = false;
                    break;
                }
                
                _avoidPlayerTimer += Time.deltaTime;
                float avoidTimerCompareValue = _timesFoundSneaking % 3 != 0 ? 11f : 21f;
                if (_avoidPlayerTimer > avoidTimerCompareValue)
                {
                    _avoidPlayerTimer = 0f;
                    if (_avoidPlayerCoroutine != null)
                    {
                        StopCoroutine(_avoidPlayerCoroutine);
                        _avoidPlayerCoroutine = null;
                    }
                    
                    SwitchBehaviourStateLocally(previousBehaviourStateIndex);
                }
                
                break;
            }

            case (int)States.KidnappingPlayer:
            {
                _dragPlayerTimer -= Time.deltaTime;
                if (_dragPlayerTimer <= 0 && !_hasTransitionedToRunningForwardsAndCarryingPlayer)
                {
                    _dragPlayerTimer = float.MaxValue; // Better than adding ANOTHER bool value to this if statement
                    netcodeController.SetAnimationTriggerClientRpc(_aloeId, AloeClient.KidnapRun);
                    StartCoroutine(TransitionToRunningForwardsAndCarryingPlayer(0.3f));
                }
                
                const float distanceInFront = -1.5f;
                Vector3 newPosition = transform.position + transform.forward * distanceInFront;
                targetPlayer.transform.position = newPosition;
                
                break;
            }

            case (int)States.ChasingEscapedPlayer:
            {
                _waitTimer -= Time.deltaTime;
                
                break;
            }
        }
    }

    /// <summary>
    /// Handles most of the main AI logic
    /// The logic in this method is not run every frame
    /// </summary>
    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer) return;
        if (StartOfRound.Instance.livingPlayers == 0 || isEnemyDead || currentBehaviourStateIndex == (int)States.Dead) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.PassiveRoaming:
            {
                // Check if a player sees the aloe
                PlayerControllerB tempPlayer = AloeUtils.GetPlayerLookingAtPosition(eye.transform, logSource: _mls);
                if (tempPlayer != null)
                {
                    _avoidingPlayer = tempPlayer;
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                // Check if a player has below "playerHealthThresholdForStalking" % of health
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (player.HasLineOfSightToPosition(eye.transform.position))
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.25f, player.playerClientId);
                    
                    if (!PlayerIsStalkable(player)) continue;
                    
                    targetPlayer = player;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    SwitchBehaviourStateLocally(States.PassivelyStalkingPlayer);
                    break;
                }

                // Check if the aloe has reached her favourite spot, so she can start roaming from that position
                if (_reachedFavouriteSpotForRoaming)
                {
                    LogDebug("Heading towards favourite position before roaming");
                    SetDestinationToPosition(_favouriteSpot);
                    if (roamMap.inProgress) StopSearch(roamMap);
                    if (Vector3.Distance(_favouriteSpot, transform.position) <= 4)
                        _reachedFavouriteSpotForRoaming = true;
                }
                else
                {
                    if (!roamMap.inProgress)
                    {
                        StartSearch(transform.position, roamMap);
                        LogDebug("Starting to roam map");
                    }
                }
                
                break;
            }

            case (int)States.AvoidingPlayer:
            {
                _avoidPlayerCoroutine ??= StartCoroutine(AvoidClosestPlayer(true));
                
                // List<PlayerControllerB> playersLookingAtAloe = WhoAreLookingAtAloe();
                // foreach (PlayerControllerB player in playersLookingAtAloe)
                // {
                //     // Todo: Handle this on the client script instead of spamming RPCs
                //     netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.4f, player.actualClientId);
                // }
                
                PlayerControllerB tempPlayer = AloeUtils.GetPlayerLookingAtPosition(eye.transform, logSource: _mls);
                if (tempPlayer != null)
                {
                    _avoidingPlayer = tempPlayer;
                    if (_avoidPlayerAudioTimer <= 0)
                    {
                        _avoidPlayerAudioTimer = 4.1f;
                        netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                    }
                    
                    _avoidPlayerTimer = 0f;
                }

                break;
            }

            case (int)States.PassivelyStalkingPlayer:
            {
                // Check if a player sees the aloe
                PlayerControllerB tempPlayer = AloeUtils.GetPlayerLookingAtPosition(eye.transform, logSource: _mls);
                if (tempPlayer != null)
                {
                    LogDebug("While PASSIVELY stalking, player was looking at Aloe, avoiding the player now");
                    _avoidingPlayer = tempPlayer;
                    _timesFoundSneaking++;
                    
                    // Greatly increase fear level if the player turns around to see the Aloe starting at them
                    if (_isStaringAtTargetPlayer && targetPlayer == _avoidingPlayer)
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.8f, _avoidingPlayer.playerClientId);
                    
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                if (roamMap.inProgress) StopSearch(roamMap);
                
                // Check if a player has below "playerHealthThresholdForHealing" % of health
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (player.HasLineOfSightToPosition(eye.transform.position))
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.5f, player.playerClientId);
                    
                    if (!PlayerIsStalkable(player)) continue;
                    
                    targetPlayer = player;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    SwitchBehaviourStateLocally(States.StalkingPlayerToKidnap);
                    break;
                }
                
                // Check if her chosen player is still alive
                if (AloeUtils.IsPlayerDead(targetPlayer))
                {
                    SwitchBehaviourStateLocally(States.PassiveRoaming);
                    break;
                }
                
                // See if the aloe can stare at the player
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= passiveStalkStaredownDistance &&
                    !Physics.Linecast(eye.position, targetPlayer.gameplayCamera.transform.position,
                        StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    LogDebug("Aloe is staring at player");
                    if (!_isStaringAtTargetPlayer) netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 1);
                    
                    moveTowardsDestination = false;
                    movingTowardsTargetPlayer = false;
                    _isStaringAtTargetPlayer = true;
                }
                else
                {
                    if (_isStaringAtTargetPlayer) netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 0, 0.1f);
                    
                    _isStaringAtTargetPlayer = false;
                    if (AloeUtils.IsPlayerReachable(agent, targetPlayer, transform, eye, viewWidth, viewRange, logSource: _mls))
                    {
                        ChooseClosestNodeToTargetPlayer();
                    }
                    else
                    {
                        Transform farAwayTransform = ChooseFarthestNodeFromPosition(_mainEntrancePosition);
                        targetNode = farAwayTransform;
                        SetDestinationToPosition(farAwayTransform.position, true);
                    } 
                }
                
                break;
            }

            case (int)States.StalkingPlayerToKidnap:
            {
                // Check if a player sees the aloe
                PlayerControllerB tempPlayer = AloeUtils.GetPlayerLookingAtPosition(eye.transform, logSource: _mls);
                if (tempPlayer != null)
                {
                    LogDebug("While stalking for kidnapping, player was looking at aloe, avoiding the player now");
                    _avoidingPlayer = tempPlayer;
                    _timesFoundSneaking++;
                    
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                if (roamMap.inProgress) StopSearch(roamMap);
                
                // Check if her chosen player is still alive
                if (AloeUtils.IsPlayerDead(targetPlayer))
                {
                    SwitchBehaviourStateLocally(States.PassiveRoaming);
                    break;
                }
                
                List<PlayerControllerB> playersLookingAtAloe = AloeUtils.GetAllPlayersLookingAtPosition(eye.transform);
                foreach (PlayerControllerB player in playersLookingAtAloe)
                {
                    netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.4f, player.actualClientId);
                }

                if (_inGrabAnimation)
                {
                    movingTowardsTargetPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position) > 1.5f;
                }
                else
                {
                    if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= 4f && !_inGrabAnimation)
                    {
                        // See if the aloe can kidnap the player
                        LogDebug("Player is close to aloe! Kidnapping him now");
                        agent.speed = 0f;
                        netcodeController.SetAnimationTriggerClientRpc(_aloeId, AloeClient.Grab);
                        _inGrabAnimation = true;
                    }
                    else if (AloeUtils.IsPlayerReachable(agent, targetPlayer, transform, eye, viewWidth, viewRange, logSource: _mls))
                    {
                        ChooseClosestNodeToTargetPlayer();
                    }
                    else
                    {
                        Transform farAwayTransform = ChooseFarthestNodeFromPosition(_mainEntrancePosition);
                        targetNode = farAwayTransform;
                        SetDestinationToPosition(farAwayTransform.position, true);
                    }
                }
                
                break;
            }

            case (int)States.KidnappingPlayer:
            {
                if (Vector3.Distance(transform.position, _favouriteSpot) <= 1)
                {
                    LogDebug("reached favourite spot while kidnapping");
                    SwitchBehaviourStateLocally(States.HealingPlayer);
                }

                List<PlayerControllerB> playersLookingAtAloe = AloeUtils.GetAllPlayersLookingAtPosition(eye.transform);
                foreach (PlayerControllerB player in playersLookingAtAloe)
                {
                    netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.4f, player.actualClientId);
                }
                
                break;
            }

            case (int)States.HealingPlayer:
            {
                // Todo: BTW this will not work for the event that all the aloe nodes are taken, and this current aloe doesnt have one
                if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
                    LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
                
                int targetPlayerMaxHealth = GetPlayerMaxHealth(targetPlayer);
                if (targetPlayer.health < targetPlayerMaxHealth)
                {
                    // First check if the current heal amount will give the player too much health
                    int healthIncrease = _healingPerInterval;
                    if (targetPlayer.health + _healingPerInterval >= targetPlayerMaxHealth)
                    {
                        healthIncrease = targetPlayerMaxHealth - targetPlayer.health;
                    }
                    
                    netcodeController.HealTargetPlayerByAmountClientRpc(_aloeId, healthIncrease);
                    LogDebug($"Healed player by amount: {healthIncrease}");
                }
                // If the player cannot be healed anymore, then switch to cuddling
                else
                {
                    SwitchBehaviourStateLocally(States.CuddlingPlayer);
                }
                
                break;
            }

            case (int)States.CuddlingPlayer:
            {
                PlayerControllerB tempPlayer = AloeUtils.GetPlayerLookingAtPosition(eye.transform, targetPlayer, logSource: _mls);
                if (tempPlayer != null)
                {
                    LookAtPosition(tempPlayer.transform.position);
                }
                else if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
                {
                    LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
                }
                
                break;
            }

            case (int)States.ChasingEscapedPlayer:
            {
                if (_waitTimer <= 0)
                {
                    if (Vector3.Distance(targetPlayer.transform.position, transform.position) <= 1.5f)
                    {
                        LogDebug("Player is close to aloe! Kidnapping him now");
                        // Todo: add grab animation
                        SwitchBehaviourStateLocally(States.KidnappingPlayer);
                        break;
                    }
                    
                    if (PlayerIsTargetable(targetPlayer)) SetMovingTowardsTargetPlayer(targetPlayer);
                    else SwitchBehaviourStateLocally(States.PassiveRoaming);
                }
                else if (AloeUtils.DoesEyeHaveLineOfSightToPosition(targetPlayer.transform.position, eye, viewWidth, viewRange, logSource: _mls))
                {
                    LookAtPosition(targetPlayer.transform.position);
                }
                
                break;
            }

            case (int)States.AttackingPlayer:
            {
                if (PlayerIsTargetable(targetPlayer)) SetMovingTowardsTargetPlayer(targetPlayer);
                else SwitchBehaviourStateLocally(States.ChasingEscapedPlayer);
                
                if (Vector3.Distance(targetPlayer.transform.position, transform.position) <= 1f)
                {
                    LogDebug("Player is close to aloe! Snapping his neck");
                    
                    netcodeController.SnapPlayerNeckClientRpc(_aloeId, targetPlayer.actualClientId);
                    ChangeTargetPlayer(_backupTargetPlayer.actualClientId);
                    SwitchBehaviourStateLocally(States.ChasingEscapedPlayer);
                }
                
                break;
            }
            
            case (int)States.Dead:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                break;
            }
        }
    }

    private void PickFavouriteSpot()
    {
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
        _favouriteSpot = Vector3.zero;
        if (_favouriteSpot == Vector3.zero)
        {
            _favouriteSpot =
                AloeUtils.GetFarthestValidNodeFromPosition(
                        agent,
                        allAINodes,
                        _mainEntrancePosition != Vector3.zero ? _mainEntrancePosition : transform.position,
                        logSource: _mls)
                    .position;
        }
        
        LogDebug($"Found a favourite spot: {_favouriteSpot}");
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
                // Todo: Add a dictionary that stores all player's distances from the Aloe per specified interval
                PlayerControllerB whoIsLookingAtAloe = AloeUtils.GetPlayerLookingAtPosition(eye.transform, targetPlayer, logSource: _mls);
                if (whoIsLookingAtAloe != null)
                {
                    LookAtPosition(whoIsLookingAtAloe.transform.position);
                }

                break;
            }
            
            default:
            {
                if (!(agent.velocity.sqrMagnitude > 0.01f)) break;
                Vector3 targetDirection = !_hasTransitionedToRunningForwardsAndCarryingPlayer &&
                                          currentBehaviourStateIndex == (int)States.KidnappingPlayer
                    ? -agent.velocity.normalized
                    : agent.velocity.normalized;
        
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
                break;
            }
        }
    }

    private IEnumerator TransitionToRunningForwardsAndCarryingPlayer(float transitionDuration)
    {
        Vector3 forwardDirection = agent.velocity.normalized;
        Quaternion initialRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation(forwardDirection);
        netcodeController.TransitionToRunningForwardsAndCarryingPlayerClientRpc(_aloeId);
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, elapsedTime / transitionDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
        _hasTransitionedToRunningForwardsAndCarryingPlayer = true;
    }

    /// <summary>
    /// Creates a bind in the AloeBoundKidnaps dictionary and calls a network event to do several things in the client for kidnapping the target player.
    /// </summary>
    /// <param name="setToInCaptivity">Whether the target player is being kidnapped or finished being kidnapped.</param>
    private void SetTargetPlayerInCaptivity(bool setToInCaptivity)
    {
        if (!IsServer) return;
        if (targetPlayer == null) return;
        if (setToInCaptivity)
        {
            if (!AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(this))
                AloeSharedData.Instance.AloeBoundKidnaps.Add(this, targetPlayer);
        }
        else {
            if (AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(this))
                AloeSharedData.Instance.AloeBoundKidnaps.Remove(this);
        }
        
        netcodeController.SetTargetPlayerInCaptivityClientRpc(_aloeId, setToInCaptivity);
    }

    /// <summary>
    /// Is called by the teleporter patch to make sure the aloe reacts appropriately when a player is teleported away
    /// </summary>
    public void SetTargetPlayerEscapedByTeleportation()
    {
        if (!IsServer) return;
        
        LogDebug("Target player escaped by teleportation!");
        SetTargetPlayerInCaptivity(false);
        ChangeTargetPlayer(69420);
        SwitchBehaviourStateLocally(States.PassiveRoaming);
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
        SwitchBehaviourStateLocally(States.ChasingEscapedPlayer);
    }
    
    /// <summary>
    /// A coroutine that continuously avoids the closest player efficiently, without lagging the game.
    /// </summary>
    /// <param name="avoidLineOfSight">Whether to not path through areas where a player can see them.</param>
    /// <returns></returns>
    private IEnumerator AvoidClosestPlayer(bool avoidLineOfSight = false)
    {
        while (true)
        {
            if (!_finishedSpottedAnimation)
            {
                yield return null;
                continue;
            }
            
            Transform farAwayTransform = _avoidingPlayer != null
                ? AloeUtils.GetFarthestValidNodeFromPosition(
                    agent, 
                    allAINodes, 
                    _avoidingPlayer.transform.position, 
                    avoidLineOfSight,
                    logSource: _mls)
                : null;
            
            if (farAwayTransform != null)
            {
                LogDebug($"Setting target node to {farAwayTransform.position}");
                SetDestinationToPosition(farAwayTransform.position);
            }
            else
            {
                SwitchBehaviourStateLocally(States.AttackingPlayer);
            }

            yield return new WaitForSeconds(5f);
        }
    }
    
    /// <summary>
    /// Chooses the closest node next to the target player.
    /// If the Aloe is close to the player, it will just approach the player normally instead of using a node.
    /// </summary>
    private void ChooseClosestNodeToTargetPlayer()
    {
        if (targetNode == null) targetNode = allAINodes[0].transform;

        Transform position = ChooseClosestNodeToPosition(targetPlayer.transform.position, true);
        if (position != null) targetNode = position;
        float distanceToPlayer = Vector3.Distance(targetPlayer.transform.position, transform.position);
        
        // If aloe is close enough to player, head towards them to attempt a kidnap
        if (distanceToPlayer <= 5)
        {
            SetMovingTowardsTargetPlayer(targetPlayer);
            return;
        }
        
        // The mostOptimalDistance variable is calculated in the ChooseClosestNodeToPosition() method in the EnemyAI class
        if (distanceToPlayer - mostOptimalDistance < 0.10000000149011612 &&
            (!PathIsIntersectedByLineOfSight(targetPlayer.transform.position, true) || distanceToPlayer < 3))
        {
            // The pathDistance variable is calculated in the PathIsIntersectedByLineOfSight() method in the EnemyAI class
            if (pathDistance > 10.0 && !_ignoredNodes.Contains(targetNode) && _ignoredNodes.Count < 4)
                _ignoredNodes.Add(targetNode);
            
            LogDebug("close to player while choosing closest node");
            movingTowardsTargetPlayer = true;
        }
        else
        {
            SetDestinationToPosition(targetNode.position);
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
        if (currentBehaviourStateIndex is (int)States.Dead) return;
        if (_takeDamageCooldown > 0) return;

        netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.Hit);
        enemyHP -= force;
        _takeDamageCooldown = 0.03f;
        if (enemyHP > 0)
        {
            switch (currentBehaviourStateIndex)
            {
                case (int)States.PassiveRoaming:
                {
                    if (playerWhoHit == null) return;
                    
                    _avoidingPlayer = playerWhoHit;
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                // If stalking, then check if player is near, and hit them.
                case (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
                {
                    // Todo: Replace this with the hit animation
                    if (playerWhoHit == null) return;
                    
                    _backupTargetPlayer = targetPlayer;
                    ChangeTargetPlayer(playerWhoHit.actualClientId);
                    SwitchBehaviourStateLocally(States.AttackingPlayer);
                    break;
                }
                
                case (int)States.KidnappingPlayer:
                {
                    if (playerWhoHit == null) break;
                    
                    SetTargetPlayerInCaptivity(false);
                    _backupTargetPlayer = targetPlayer;
                    ChangeTargetPlayer(playerWhoHit.actualClientId);
                    SwitchBehaviourStateLocally(States.AttackingPlayer);
                    
                    break;
                }

                case (int)States.HealingPlayer or (int)States.CuddlingPlayer:
                {
                    if (playerWhoHit == null) break;
                    
                    SetTargetPlayerInCaptivity(false);
                    _backupTargetPlayer = targetPlayer;
                    ChangeTargetPlayer(playerWhoHit.actualClientId);
                    netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing, true);
                    SwitchBehaviourStateLocally(States.AttackingPlayer);
                    
                    break;
                }
                
                case (int)States.AttackingPlayer:
                {
                    if (playerWhoHit != targetPlayer && playerWhoHit != null)
                    {
                        ChangeTargetPlayer(playerWhoHit.actualClientId);
                    }
                    
                    break; 
                }
            }
        }
        else
        {
            netcodeController.EnterDeathStateClientRpc(_aloeId);
            KillEnemyServerRpc(false);
            SwitchBehaviourStateLocally(States.Dead);
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
        netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.Stun, true);
        netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Stunned, true);
        netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, false);
        netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 0, 0f);
        
        switch (currentBehaviourStateIndex)
        { 
            case (int)States.PassiveRoaming:
            {
                if (setStunnedByPlayer == null) break;
                
                _avoidingPlayer = setStunnedByPlayer;
                SwitchBehaviourStateLocally(States.AvoidingPlayer);
                break; 
            }
            
            case (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
            {
                if (setStunnedByPlayer == null) break;
                
                _avoidingPlayer = setStunnedByPlayer;
                SwitchBehaviourStateLocally(States.AvoidingPlayer);
                break;
            }
            
            case (int)States.KidnappingPlayer:
            {
                SetTargetPlayerInCaptivity(false);
                _avoidingPlayer = setStunnedByPlayer;
                SwitchBehaviourStateLocally(States.AvoidingPlayer);
                
                break;
            }

            case (int)States.HealingPlayer or (int)States.CuddlingPlayer:
            {
                if (setStunnedByPlayer == null) break;
                _backupTargetPlayer = targetPlayer;
                ChangeTargetPlayer(setStunnedByPlayer.actualClientId);
                SwitchBehaviourStateLocally(States.AttackingPlayer);
                break;
            }
            
            case (int)States.AttackingPlayer:
            {
                if (setStunnedByPlayer != targetPlayer && setStunnedByPlayer != null)
                {
                    targetPlayer = setStunnedByPlayer;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, setStunnedByPlayer.actualClientId);
                }
                
                break;
            }
        }
    }

    /// <summary>
    /// Determines whether the given player is "stalkable" or not.
    /// </summary>
    /// <param name="player">The player to check if stalkable.</param>
    /// <returns>Whether the given player is stalkable</returns>
    private bool PlayerIsStalkable(PlayerControllerB player)
    {
        int healthThreshold = currentBehaviourStateIndex == (int)States.PassiveRoaming
            ? playerHealthThresholdForStalking
            : playerHealthThresholdForHealing;
        
        return player.health <= healthThreshold && AloeUtils.IsPlayerTargetable(player);
    }

    /// <summary>
    /// Makes the Aloe look at the given position by rotating smoothly.
    /// </summary>
    /// <param name="position">The position to look at.</param>
    /// <param name="rotationSpeed">The speed at which to rotate at.</param>
    private void LookAtPosition(Vector3 position, float rotationSpeed = 30f)
    {
        Vector3 direction = (position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    /// <summary>
    /// Determines whether a player is looking at the Aloe.
    /// </summary>
    /// <returns>Whether a player is looking at the Aloe</returns>
    private bool IsPlayerLookingAtAloe()
    {
        return StartOfRound.Instance.allPlayerScripts.Where(player => !player.isPlayerDead && player.isInsideFactory).Any(player => player.HasLineOfSightToPosition(transform.position + Vector3.up * 0.5f, 30f));
    }
    
    

    /// <summary>
    /// Switches to the kidnapping state.
    /// This function is called by a network event which is called by an animation event.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleGrabTargetPlayer(string receivedAloeId)
    {
        if (!IsServer) return;
        if (_aloeId != receivedAloeId) return;
        if (currentBehaviourStateIndex != (int)States.StalkingPlayerToKidnap) return;
        
        LogDebug("Grab target player network event triggered");
        SwitchBehaviourStateLocally((int)States.KidnappingPlayer);
    }

    /// <summary>
    /// Makes the Aloe start to avoid the player when the spotted animation is complete.
    /// This function is called by a network event which is called by an animation state behaviour.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleSpottedAnimationComplete(string receivedAloeId)
    {
        if (!IsServer) return;
        if (_aloeId != receivedAloeId) return;
        if (currentBehaviourStateIndex != (int)States.AvoidingPlayer) return;
        
        LogDebug("Spotted animation complete network event triggered");
        netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 0, 0.8f);
        _finishedSpottedAnimation = true;
        moveTowardsDestination = true;
    }

    /// <summary>
    /// Resets the required variables and runs setup functions for each particular behaviour state
    /// </summary>
    /// <param name="state">The state to switch to.</param>
    private void InitializeState(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.Spawning: // 0
            {
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 50f;
                
                break;
            }
            
            case (int)States.PassiveRoaming: // 1
            {
                _agentMaxSpeed = 2f;
                _agentMaxAcceleration = 2f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 2f;
                _avoidPlayerCoroutine = null;
                _avoidingPlayer = null;
                moveTowardsDestination = true;
                _reachedFavouriteSpotForRoaming = false;

                if (_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, false);
                    _currentlyHasDarkSkin = false;
                }
                
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, false);
                netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 0, 0.5f);
                
                break;
            }

            case (int)States.AvoidingPlayer: // 2
            {
                _agentMaxSpeed = 9f;
                _agentMaxAcceleration = 100f;
                _avoidPlayerTimer = 0f;
                _finishedSpottedAnimation = false;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                openDoorSpeedMultiplier = 10f;
                _avoidPlayerAudioTimer = 4.1f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                
                netcodeController.SetAnimationTriggerClientRpc(_aloeId, AloeClient.Spotted);
                netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 0.8f, 0.5f);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                
                break;
            }

            case (int)States.PassivelyStalkingPlayer: // 3
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 70f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _avoidingPlayer = null;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 4f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;

                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, true);
                
                break; 
            }

            case (int)States.StalkingPlayerToKidnap: // 4
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _inGrabAnimation = false;
                _avoidingPlayer = null;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 4f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;
                
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, true);
                
                break;
            }

            case (int)States.KidnappingPlayer: // 6
            {
                _agentMaxSpeed = 8f;
                _agentMaxAcceleration = 20f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _avoidingPlayer = null;
                _hasTransitionedToRunningForwardsAndCarryingPlayer = false;
                _avoidPlayerTimer = 0f;
                _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;
                openDoorSpeedMultiplier = 10f;
                _avoidPlayerCoroutine = null;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _ignoredNodes.Clear();
                
                // Spawn fake player body ragdoll
                GameObject fakePlayerBodyRagdollGameObject = 
                    Instantiate(
                        AloeHandler.Instance.Assets.FakePlayerBodyRagdollPrefab, 
                        targetPlayer.thisPlayerBody.position + Vector3.up * 1.25f, 
                        targetPlayer.thisPlayerBody.rotation, 
                        null);

                NetworkObject fakePlayerBodyRagdollNetworkObject =
                    fakePlayerBodyRagdollGameObject.GetComponent<NetworkObject>();
                fakePlayerBodyRagdollNetworkObject.Spawn();
                
                ChangeTargetPlayer(targetPlayer.actualClientId);
                SetTargetPlayerInCaptivity(true);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, false);
                netcodeController.SpawnFakePlayerBodyRagdollClientRpc(_aloeId, fakePlayerBodyRagdollNetworkObject);
                netcodeController.SetTargetPlayerAbleToEscapeClientRpc(_aloeId, false);
                netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 2.5f, targetPlayer.actualClientId);
                netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.SnatchAndDrag);

                if (!AloeUtils.IsPathValid(agent, _favouriteSpot, logSource: _mls))
                {
                    LogDebug("When initializing kidnapping, no path was found to the Aloe's favourite spot.");
                    SwitchBehaviourStateLocally(States.HealingPlayer);
                    break;
                }
                
                SetDestinationToPosition(_favouriteSpot);
                
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                break;
            }

            case (int)States.HealingPlayer: // 7
            {
                agent.speed = 0;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _avoidingPlayer = null;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 4f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;

                netcodeController.SetTargetPlayerAbleToEscapeClientRpc(_aloeId, true);
                netcodeController.UnMuffleTargetPlayerVoiceClientRpc(_aloeId);
                if (targetPlayer.health == GetPlayerMaxHealth(targetPlayer))
                {
                    SwitchBehaviourStateLocally(States.CuddlingPlayer);
                    break;
                }
                
                if (_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, false);
                    _currentlyHasDarkSkin = false;
                }
                
                // Start healing the player
                LogDebug("Starting to heal the player");
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, true);
                ChangeTargetPlayer(targetPlayer.actualClientId);
                    
                // Calculate the heal amount per AIInterval
                int targetPlayerMaxHealth = GetPlayerMaxHealth(targetPlayer);
                float baseHealingRate = 100f / timeItTakesToFullyHealPlayer;
                float healingRate = baseHealingRate * targetPlayerMaxHealth / 100f;
                _healingPerInterval = Mathf.CeilToInt(healingRate * AIIntervalTime);
                
                // Calculate the total time it takes to heal the player
                float totalHealingTime = (targetPlayerMaxHealth - targetPlayer.health) / healingRate;
                netcodeController.PlayHealingVfxClientRpc(_aloeId, totalHealingTime);
                
                targetPlayer.HealServerRpc(); // Doesn't actually heal them, just makes them not bleed anymore
                
                break;
            }

            case (int)States.CuddlingPlayer: // 8
            {
                agent.speed = 0;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _avoidingPlayer = null;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 4f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;
                
                if (_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, false);
                    _currentlyHasDarkSkin = false;
                }
                
                netcodeController.UnMuffleTargetPlayerVoiceClientRpc(_aloeId);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, true);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                
                break;
            }

            case (int)States.ChasingEscapedPlayer: // 9
            {
                _agentMaxSpeed = 6f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _avoidingPlayer = null;
                movingTowardsTargetPlayer = false;
                _inGrabAnimation = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 2f;
                _waitTimer = 2f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;
                
                netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.Chase);
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, false);
                
                break;
            }

            case (int)States.AttackingPlayer: // 10
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _avoidingPlayer = null;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 2f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;
                
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Healing, false);
                netcodeController.SetAnimationBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                
                break;
            }
            
            case (int)States.Dead: // 11
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;

                _agentMaxSpeed = 0;
                _agentMaxAcceleration = 0;
                movingTowardsTargetPlayer = false;
                moveTowardsDestination = false;
                agent.speed = 0;
                agent.enabled = false;
                isEnemyDead = true;
                _isStaringAtTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                _avoidingPlayer = null;
                
                SetTargetPlayerInCaptivity(false);

                break;
            }
        }
    }

    /// <summary>
    /// Switches to the given behaviour state represented by the state enum
    /// </summary>
    /// <param name="state">The state enum to change to.</param>
    private void SwitchBehaviourStateLocally(States state)
    {
        SwitchBehaviourStateLocally((int)state);
    }

    /// <summary>
    /// Switches to the given behaviour state represented by an integer
    /// </summary>
    /// <param name="state">The state integer to change to.</param>
    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer || currentBehaviourStateIndex == state) return;
        LogDebug($"Switched to behaviour state {state}!");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        InitializeState(state);
        netcodeController.CurrentBehaviourStateIndex.Value = currentBehaviourStateIndex;
        LogDebug($"Switch to behaviour state {state} complete!");
    }

    /// <summary>
    /// Gets the max health of the given player
    /// This is needed because mods may increase the max health of a player
    /// </summary>
    /// <param name="player">The player to get the max health.</param>
    /// <returns>The player's max health</returns>
    private static int GetPlayerMaxHealth(PlayerControllerB player)
    {
        if (AloeSharedData.Instance.PlayersMaxHealth.ContainsKey(player))
        {
            return AloeSharedData.Instance.PlayersMaxHealth[player];
        }

        return -1;
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
            _isStaringAtTargetPlayer ||
            currentBehaviourStateIndex == (int)States.Dead ||
            currentBehaviourStateIndex == (int)States.Spawning)
        {
            agent.speed = 0;
            agent.acceleration = _agentMaxAcceleration;
        }
        else if (_inGrabAnimation)
        {
            agent.speed = 1;
            agent.acceleration = _agentMaxAcceleration;
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
        agent.speed = Mathf.Lerp(agent.speed, _agentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, _agentMaxAcceleration, accelerationAdjustment);
    }

    private void HandleSpawnAnimationComplete(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        if (!IsServer) return;
        
        LogDebug($"In {nameof(HandleSpawnAnimationComplete)}");
        SwitchBehaviourStateLocally(States.PassiveRoaming);
    }
    
    private void ChangeTargetPlayer(ulong playerClientId)
    {
        if (!IsServer) return;
        targetPlayer = playerClientId == 69420 ? null : StartOfRound.Instance.allPlayerScripts[playerClientId];
        netcodeController.ChangeTargetPlayerClientRpc(_aloeId, playerClientId);
        LogDebug($"Target player is now: {(targetPlayer == null ? "null" : targetPlayer.playerUsername)}");
    }

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || _networkEventsSubscribed) return;
        
        netcodeController.OnTargetPlayerEscaped += HandleTargetPlayerEscaped;
        netcodeController.OnGrabTargetPlayer += HandleGrabTargetPlayer;
        netcodeController.OnSpottedAnimationComplete += HandleSpottedAnimationComplete;
        netcodeController.OnSpawnAnimationComplete += HandleSpawnAnimationComplete;

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
        netcodeController.OnSpottedAnimationComplete -= HandleSpottedAnimationComplete;
        netcodeController.OnSpawnAnimationComplete -= HandleSpawnAnimationComplete;

        _networkEventsSubscribed = false;
    }

    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        if (!IsServer) return;

        maxRoamingRadius = Config.MaxRoamingRadius;
        viewWidth = Config.ViewWidth;
        viewRange = Config.ViewRange;
        playerHealthThresholdForStalking = Config.PlayerHealthThresholdForStalking;
        playerHealthThresholdForHealing = Config.PlayerHealthThresholdForHealing;
        timeItTakesToFullyHealPlayer = Config.TimeItTakesToFullyHealPlayer;
        passiveStalkStaredownDistance = Config.PassiveStalkStaredownDistance;
        
        roamMap.searchWidth = maxRoamingRadius;
        
        netcodeController.InitializeConfigValuesClientRpc(_aloeId);
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log.</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo($"State:{currentBehaviourStateIndex}, {msg}");
        #endif
    }
}