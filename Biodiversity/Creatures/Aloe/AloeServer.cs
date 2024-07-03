using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Biodiversity.General;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;
using Logger = BepInEx.Logging.Logger;
using Object = UnityEngine.Object;

namespace Biodiversity.Creatures.Aloe;

public class AloeServer : BiodiverseAI
{
    private ManualLogSource _mls;
    private string _aloeId;
    
    [field: HideInInspector] [field: SerializeField] public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;

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
        PassiveRoaming,
        AvoidingPlayer,
        PassivelyStalkingPlayer,
        StalkingPlayerToKidnap,
        KidnappingPlayer,
        HealingPlayer,
        CuddlingPlayer,
        ChasingEscapedPlayer,
        AttackingPlayer,
        Dead
    }

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    public void OnEnable()
    {
        netcodeController.OnTargetPlayerEscaped += HandleTargetPlayerEscaped;
        netcodeController.OnGrabTargetPlayer += HandleGrabTargetPlayer;
    }

    /// <summary>
    /// Unsubscribe to the network events when the creature is dead.
    /// </summary>
    public void OnDisable()
    {
        netcodeController.OnTargetPlayerEscaped -= HandleTargetPlayerEscaped;
        netcodeController.OnGrabTargetPlayer -= HandleGrabTargetPlayer;
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;

        _aloeId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Server {_aloeId}");

        netcodeController = GetComponent<AloeNetcodeController>();
        if (netcodeController == null)
        {
            _mls.LogError("Netcode Controller is null, aborting spawn");
            Destroy(gameObject);
            return;
        }
        
        UnityEngine.Random.InitState(StartOfRound.Instance.randomMapSeed + _aloeId.GetHashCode());
        netcodeController.SyncAloeIdClientRpc(_aloeId);
        InitializeConfigValues();

        _mainEntrancePosition = RoundManager.FindMainEntrancePosition();
        favoriteSpot = AloeSharedData.Instance.BrackenRoomPosition != null ? AloeSharedData.Instance.BrackenRoomPosition : ChooseFarthestNodeFromPosition(_mainEntrancePosition);
        agent.updateRotation = false;
        LogDebug($"Found a favourite spot: {favoriteSpot.position}");
        
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
            netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Stunned, false);
            _inStunAnimation = false;
        }
        else if (_inStunAnimation) return;

        switch (currentBehaviourStateIndex)
        {
            case (int)States.PassiveRoaming:
            {
                // Check if a player sees the aloe
                if (IsPlayerLookingAtAloe())
                {
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                }

                // Check if the aloe has reached her favourite spot, so she can start roaming from that position
                if (!_reachedFavouriteSpotForRoaming && Vector3.Distance(favoriteSpot.position, transform.position) <= 4)
                {
                    if (HasLineOfSightToPosition(favoriteSpot.position, viewWidth, viewRange, proximityAwareness))
                        _reachedFavouriteSpotForRoaming = true;
                }
                
                break;
            }

            case (int)States.AvoidingPlayer:
            {
                _avoidPlayerAudioTimer -= Time.deltaTime;

                // Make the Aloe stay still until the spotted animation is finished
                if (!_finishedSpottedAnimation)
                {
                    if (_avoidPlayerTimer >= 0)
                    {
                        LogDebug("Finished spotted animation");
                        _finishedSpottedAnimation = true;
                        moveTowardsDestination = true;
                    }
                    else
                    {
                        _avoidPlayerTimer += Time.deltaTime;
                        moveTowardsDestination = false;
                    }
                }
                
                if (!IsPlayerLookingAtAloe())
                {
                    _avoidPlayerTimer += Time.deltaTime;
                }
                else
                {
                    PlayerControllerB tempPlayer = WhoIsLookingAtAloe();
                    if (tempPlayer != null) _avoidingPlayer = tempPlayer;

                    if (_avoidPlayerAudioTimer <= 0)
                    {
                        _avoidPlayerAudioTimer = 4.1f;
                        netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                    }
                    
                    _avoidPlayerTimer = 0f;
                }
                
                float avoidTimerCompareValue = _timesFoundSneaking % 3 != 0 ? 11f : 24f;
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

            case (int)States.PassivelyStalkingPlayer:
            {
                // Check if a player sees the aloe
                _avoidingPlayer = WhoIsLookingAtAloe();
                if (_avoidingPlayer != null)
                {
                    LogDebug("While PASSIVELY stalking, player was looking at aloe, avoiding the player now");
                    netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                    _timesFoundSneaking++;
                    
                    if (_isStaringAtTargetPlayer && targetPlayer == _avoidingPlayer)
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.8f, _avoidingPlayer.playerClientId);
                    
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                break;
            }

            case (int)States.StalkingPlayerToKidnap:
            {
                // Check if a player sees the aloe
                _avoidingPlayer = WhoIsLookingAtAloe();
                if (_avoidingPlayer != null)
                {
                    LogDebug("While stalking for kidnapping, player was looking at aloe, avoiding the player now");
                    _timesFoundSneaking++;
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
                    break;
                }
                
                break;
            }

            case (int)States.KidnappingPlayer:
            {
                _dragPlayerTimer -= Time.deltaTime;
                if (_dragPlayerTimer <= 0 && !_hasTransitionedToRunningForwardsAndCarryingPlayer)
                {
                    _dragPlayerTimer = float.MaxValue; // Better than adding ANOTHER bool value to this if statement
                    netcodeController.SetTriggerClientRpc(_aloeId, AloeClient.KidnapRun);
                    StartCoroutine(TransitionToRunningForwardsAndCarryingPlayer(0.3f));
                }
                
                const float distanceInFront = -1.5f;
                Vector3 newPosition = transform.position + transform.forward * distanceInFront;
                targetPlayer.transform.position = newPosition;
                
                
                // if (_carryingPlayer)
                // {
                //     const float distanceInFront = -0.8f;
                //     Vector3 newPosition = transform.position + transform.forward * distanceInFront;
                //     targetPlayer.transform.position = newPosition;
                // }
                
                break;
            }

            case (int)States.HealingPlayer:
            {
                break;
            }

            case (int)States.CuddlingPlayer:
            {
                break;
            }

            case (int)States.ChasingEscapedPlayer:
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer > 0) return;
                if (Vector3.Distance(targetPlayer.transform.position, transform.position) <= 1.5f)
                {
                    LogDebug("Player is close to aloe! Kidnapping him now");
                    SwitchBehaviourStateLocally(States.KidnappingPlayer);
                    break;
                }
                
                break;
            }

            case (int)States.AttackingPlayer:
            {
                if (Vector3.Distance(targetPlayer.transform.position, transform.position) <= 1f)
                {
                    LogDebug("Player is close to aloe! Snapping his neck");
                    
                    netcodeController.SnapPlayerNeckClientRpc(_aloeId, targetPlayer.actualClientId);
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, _backupTargetPlayer.actualClientId);
                    targetPlayer = _backupTargetPlayer;
                    SwitchBehaviourStateLocally(States.ChasingEscapedPlayer);
                    break;
                }
                
                break;
            }
            
            case (int)States.Dead:
            {
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
                // Check if a player has below "playerHealthThresholdForStalking" % of health
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (!PlayerIsStalkable(player)) continue;
                    
                    if (player.HasLineOfSightToPosition(eye.transform.position))
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.2f, player.playerClientId);
                    
                    targetPlayer = player;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    SwitchBehaviourStateLocally(States.PassivelyStalkingPlayer);
                    break;
                }

                if (!_reachedFavouriteSpotForRoaming)
                {
                    LogDebug("Heading towards favourite position before roaming");
                    SetDestinationToPosition(favoriteSpot.position);
                    if (roamMap.inProgress) StopSearch(roamMap);
                }
                
                // If not already roaming, then start the roam search routine
                else if (!roamMap.inProgress)
                {
                    LogDebug("Starting to roam map");
                    StartSearch(favoriteSpot.position, roamMap);
                }
                
                break;
            }

            case (int)States.AvoidingPlayer:
            {
                _avoidPlayerCoroutine ??= StartCoroutine(AvoidClosestPlayer(true));
                
                List<PlayerControllerB> playersLookingAtAloe = WhoAreLookingAtAloe();
                foreach (PlayerControllerB player in playersLookingAtAloe)
                {
                    netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.4f, player.actualClientId);
                }

                break;
            }

            case (int)States.PassivelyStalkingPlayer:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                
                // Check if a player has below "playerHealthThresholdForHealing" % of health
                foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
                {
                    if (!PlayerIsStalkable(player)) continue;
                    
                    if (player.HasLineOfSightToPosition(eye.transform.position))
                        netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.2f, player.playerClientId);
                    
                    targetPlayer = player;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    SwitchBehaviourStateLocally(States.StalkingPlayerToKidnap);
                    break;
                }
                
                // Check if her chosen player is still alive
                if (IsPlayerDead(targetPlayer))
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
                    //LookAtPlayer(targetPlayer);
                    if (!_isStaringAtTargetPlayer) netcodeController.ChangeLookAimConstraintWeightClientRpc(_aloeId, 1);
                    
                    moveTowardsDestination = false;
                    movingTowardsTargetPlayer = false;
                    _isStaringAtTargetPlayer = true;
                }
                else
                {
                    _isStaringAtTargetPlayer = false;
                    if (IsTargetPlayerReachable())
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
                if (roamMap.inProgress) StopSearch(roamMap);
                
                // Check if her chosen player is still alive
                if (IsPlayerDead(targetPlayer))
                {
                    SwitchBehaviourStateLocally(States.PassiveRoaming);
                    break;
                }
                
                List<PlayerControllerB> playersLookingAtAloe = WhoAreLookingAtAloe();
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
                    if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= 2f && !_inGrabAnimation)
                    {
                        // See if the aloe can kidnap the player
                        LogDebug("Player is close to aloe! Kidnapping him now");
                        netcodeController.SetTriggerClientRpc(_aloeId, AloeClient.Grab);
                        _inGrabAnimation = true;
                    }
                    else if (IsTargetPlayerReachable())
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
                if (Vector3.Distance(transform.position, favoriteSpot.position) < 1)
                {
                    LogDebug("reached favourite spot while kidnapping");
                    netcodeController.UnMuffleTargetPlayerVoiceClientRpc(_aloeId);

                    //_carryingPlayer = false;
                    SwitchBehaviourStateLocally(States.HealingPlayer);
                }

                List<PlayerControllerB> playersLookingAtAloe = WhoAreLookingAtAloe();
                foreach (PlayerControllerB player in playersLookingAtAloe)
                {
                    netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 0.4f, player.actualClientId);
                }
                
                break;
            }

            case (int)States.HealingPlayer:
            {
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
                break;
            }

            case (int)States.ChasingEscapedPlayer:
            {
                if (_waitTimer <= 0)
                {
                    if (PlayerIsTargetable(targetPlayer)) SetMovingTowardsTargetPlayer(targetPlayer);
                    else SwitchBehaviourStateLocally(States.PassiveRoaming);
                }
                
                break;
            }

            case (int)States.AttackingPlayer:
            {
                if (PlayerIsTargetable(targetPlayer)) SetMovingTowardsTargetPlayer(targetPlayer);
                else SwitchBehaviourStateLocally(States.ChasingEscapedPlayer);
                
                break;
            }
            
            case (int)States.Dead:
            {
                if (roamMap.inProgress) StopSearch(roamMap);
                break;
            }
        }
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
                PlayerControllerB whoIsLookingAtAloe = WhoIsLookingAtAloe(targetPlayer);
                if (whoIsLookingAtAloe != null)
                {
                    LookAtPlayer(whoIsLookingAtAloe);
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
    /// <param name="setToInCaptivity">Whether the target player is being kidnapped or finished being kidnapped</param>
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
        netcodeController.ChangeTargetPlayerClientRpc(_aloeId, 69420);
        SwitchBehaviourStateLocally(States.PassiveRoaming);
    }

    /// <summary>
    /// Is called on a network event when player manages to escape by mashing keys on their keyboard.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID</param>
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
    /// <param name="avoidLineOfSight">Whether to not path through areas where a player can see them</param>
    /// <returns></returns>
    private IEnumerator AvoidClosestPlayer(bool avoidLineOfSight = false)
    {
        while (true)
        {
            Transform farAwayTransform = _avoidingPlayer != null
                ? ChooseFarthestNodeFromPosition(_avoidingPlayer.transform.position, avoidLineOfSight)
                : null;
            
            if (farAwayTransform != null && mostOptimalDistance > 5.0 && Physics.Linecast(farAwayTransform.position,
                    _avoidingPlayer.gameplayCamera.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault,
                    QueryTriggerInteraction.Ignore))
            {
                LogDebug($"Setting target node to {farAwayTransform.position}");
                targetNode = farAwayTransform;
                SetDestinationToPosition(targetNode.position);
            }
            else
            {
                // Still setting a far away node, but a player might see the aloe
                farAwayTransform = _avoidingPlayer != null
                    ? ChooseFarthestNodeFromPosition(_avoidingPlayer.transform.position)
                    : null;
                
                if (farAwayTransform != null)
                {
                    targetNode = farAwayTransform;
                    if (!SetDestinationToPosition(targetNode.position))
                    {
                        SetDestinationToPosition(favoriteSpot.position, true);
                    }
                }
                else
                {
                    SetDestinationToPosition(favoriteSpot.position, true);
                }
            }

            yield return new WaitForSeconds(5f);
        }
    }

    /// <summary>
    /// Returns whether 1 or more players are inside the facility/factory.
    /// </summary>
    /// <returns>Whether 1 or more players are inside the facility/factory</returns>
    private bool IsAnyPlayerInsideTheFacility()
    {
        return StartOfRound.Instance.allPlayerScripts.Any(IsPlayerTargetable);
    }

    /// <summary>
    /// Detects whether the target player is reachable by a path.
    /// </summary>
    /// <param name="bufferDistance"></param>
    /// <param name="requireLineOfSight"></param>
    /// <param name="viewWidth"></param>
    /// <returns>Whether the target player is reachable</returns>
    private bool IsTargetPlayerReachable(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 0f)
    {
        mostOptimalDistance = 2000f;
        if (viewWidth == 0) viewWidth = this.viewWidth;
        if (PlayerIsTargetable(targetPlayer) && !PathIsIntersectedByLineOfSight(targetPlayer.transform.position, avoidLineOfSight: false) && (!requireLineOfSight || HasLineOfSightToPosition(targetPlayer.gameplayCamera.transform.position, viewWidth, viewRange)))
        {
            tempDist = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (tempDist < (double) mostOptimalDistance)
            {
                mostOptimalDistance = tempDist;
            }
        }

        LogDebug($"Is target player reachable: {targetPlayer != null && bufferDistance > 0.0 && Mathf.Abs(mostOptimalDistance - Vector3.Distance(transform.position, targetPlayer.transform.position)) < (double)bufferDistance}");
        return targetPlayer != null && bufferDistance > 0.0 &&
               Mathf.Abs(mostOptimalDistance - Vector3.Distance(transform.position, targetPlayer.transform.position)) <
               (double)bufferDistance;
    }
    
    /// <summary>
    /// Checks if the AI can construct a path to the given position.
    /// </summary>
    /// <param name="position">The position to path to</param>
    /// <returns>Whether it can path to the position or not</returns>
    private bool IsPathReachable(Vector3 position)
    {
        position = RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 1.75f);
        path1 = new NavMeshPath();
        
        // ReSharper disable once UseIndexFromEndExpression
        return agent.CalculatePath(position, path1) && !(Vector3.Distance(path1.corners[path1.corners.Length - 1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5499999523162842);
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
    /// <param name="force">The amount of damage that was done by the hit</param>
    /// <param name="playerWhoHit">The player object that hit the Aloe, if it was hit by a player</param>
    /// <param name="playHitSFX">Don't use this</param>
    /// <param name="hitId">The ID of hit which dealt the damage</param>
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
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    break;
                }
                
                // If stalking, then check if player is near, and hit them.
                case (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
                {
                    // Todo: Replace this with the hit animation
                    if (playerWhoHit != null)
                    {
                        playerWhoHit.DamagePlayer(20);
                        _avoidingPlayer = playerWhoHit;
                        SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    }
                    break;
                }
                
                case (int)States.KidnappingPlayer:
                {
                    SetTargetPlayerInCaptivity(false);
                    if (playerWhoHit != null)
                    {
                        playerWhoHit.DamagePlayer(20);
                        _avoidingPlayer = playerWhoHit;
                        SwitchBehaviourStateLocally(States.AvoidingPlayer);
                    }
                    
                    break;
                }

                case (int)States.HealingPlayer or (int)States.CuddlingPlayer:
                {
                    if (playerWhoHit == null) break;
                    // Todo: cancel healing animation
                    _backupTargetPlayer = targetPlayer;
                    targetPlayer = playerWhoHit;
                    netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.InterruptedHealing, true);
                    SwitchBehaviourStateLocally(States.AttackingPlayer);
                    break;
                }
                
                case (int)States.AttackingPlayer:
                {
                    if (playerWhoHit != targetPlayer && playerWhoHit != null)
                    {
                        targetPlayer = playerWhoHit;
                        netcodeController.ChangeTargetPlayerClientRpc(_aloeId, playerWhoHit.actualClientId);
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
    /// <param name="setToStunned">Not really sure what this is for</param>
    /// <param name="setToStunTime">The time that the aloe is going to be stunned for</param>
    /// <param name="setStunnedByPlayer">The player that the Aloe was stunned by</param>
    public override void SetEnemyStunned(
        bool setToStunned,
        float setToStunTime = 1f,
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;

        _inStunAnimation = true;
        netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.Stun, true);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Stunned, true);
        netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, false);
        
        switch (currentBehaviourStateIndex)
        { 
            case (int)States.PassiveRoaming:
            {
                SwitchBehaviourStateLocally(States.AvoidingPlayer);
                break; 
            }
            
            case (int)States.PassivelyStalkingPlayer or (int)States.StalkingPlayerToKidnap:
            {
                if (setStunnedByPlayer != null)
                {
                    _avoidingPlayer = setStunnedByPlayer;
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                }
                break;
            }
            
            case (int)States.KidnappingPlayer:
            {
                SetTargetPlayerInCaptivity(false);
                if (setStunnedByPlayer != null)
                {
                    _avoidingPlayer = setStunnedByPlayer;
                    SwitchBehaviourStateLocally(States.AvoidingPlayer);
                }
                
                break;
            }

            case (int)States.HealingPlayer or (int)States.CuddlingPlayer:
            {
                if (setStunnedByPlayer == null) break;
                _backupTargetPlayer = targetPlayer;
                targetPlayer = setStunnedByPlayer;
                netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
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
    /// <param name="player"></param>
    /// <returns>Whether the given</returns>
    private bool PlayerIsStalkable(PlayerControllerB player)
    {
        int healthThreshold = currentBehaviourStateIndex == (int)States.PassiveRoaming
            ? playerHealthThresholdForStalking
            : playerHealthThresholdForHealing;
        
        return player.health <= healthThreshold && !AloeSharedData.Instance.AloeBoundKidnaps.ContainsValue(player) && IsPlayerTargetable(player);
    }

    /// <summary>
    /// Returns whether a player is targetable.
    /// This method is a simplified version of Zeeker's function, it's a bit doo doo.
    /// </summary>
    /// <param name="player">The player to check whether they are targetable</param>
    /// <returns>Whether the target player is targetable</returns>
    private bool IsPlayerTargetable(PlayerControllerB player)
    {
        if (player == null) return false;
        return !IsPlayerDead(player) &&
               player.isInsideFactory &&
               !(player.sinkingValue >= 0.7300000190734863) &&
               !AloeSharedData.Instance.AloeBoundKidnaps.ContainsKey(this);
    }
    
    /// <summary>
    /// Returns whether the given player is dead.
    /// </summary>
    /// <param name="player">The player to check if they are dead</param>
    /// <returns>Whether the player is dead</returns>
    private static bool IsPlayerDead(PlayerControllerB player)
    {
        if (player == null) return true;
        return player.isPlayerDead || !player.isPlayerControlled;
    }

    /// <summary>
    /// Makes the Aloe look at the player by rotating smoothly.
    /// </summary>
    /// <param name="player">The player to look at</param>
    /// <param name="rotationSpeed">The speed at which to rotate at</param>
    private void LookAtPlayer(Component player, float rotationSpeed = 30f)
    {
        Vector3 direction = (player.transform.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }
    
    /// <summary>
    /// Returns whether the Aloe has line of sight to the given position.
    /// </summary>
    /// <param name="pos">The position to check whether the aloe has line of sight of</param>
    /// <param name="width">The aloe's view width</param>
    /// <param name="range">The aloe's view range</param>
    /// <param name="proximityAwareness1">The proximity awareness of the aloe</param>
    /// <returns>Whether the aloe has line of sight to the given position or not</returns>
    private bool HasLineOfSightToPosition(Vector3 pos,
        float width = 45f,
        int range = 60,
        float proximityAwareness1 = -1f)
    {
        return Vector3.Distance(eye.position, pos) < range && !Physics.Linecast(eye.position, pos, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && (Vector3.Angle(eye.forward, pos - eye.position) < width || Vector3.Distance(transform.position, pos) < proximityAwareness1);
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
    /// Determines whether a player is looking at the Aloe, and returns the player object.
    /// </summary>
    /// <param name="ignorePlayer">A player that you can exclude from the process</param>
    /// <returns>the player object looking at the Aloe</returns>
    private PlayerControllerB WhoIsLookingAtAloe(Object ignorePlayer = null)
    {
        PlayerControllerB closestPlayer = null;
        float closestDistance = float.MaxValue;
        bool isThereAPlayerToIgnore = ignorePlayer != null;

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (player.isPlayerDead || !player.isInsideFactory) continue;
            if (isThereAPlayerToIgnore)
            {
                if (ignorePlayer == player) continue;
            }
            
            if (!player.HasLineOfSightToPosition(transform.position, 50f)) continue;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (!(distance < closestDistance)) continue;
            
            closestPlayer = player;
            closestDistance = distance;
        }

        return closestPlayer;
    }
    
    /// <summary>
    /// Returns a list of all the players who are currently looking at the Aloe.
    /// </summary>
    /// <returns>A list of players who are looking at the Aloe</returns>
    private List<PlayerControllerB> WhoAreLookingAtAloe()
    {
        List<PlayerControllerB> players = [];
        players = StartOfRound.Instance.allPlayerScripts.Where(IsPlayerTargetable).Where(player => player.HasLineOfSightToPosition(transform.position, 50f)).Aggregate(players, (current, player) => [..current.Append(player)]);

        return players;
    }

    /// <summary>
    /// Switches to the kidnapping state.
    /// Is called by a network event which is called by an animation event.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID</param>
    private void HandleGrabTargetPlayer(string receivedAloeId)
    {
        if (!IsServer) return;
        if (_aloeId != receivedAloeId) return;
        if (currentBehaviourStateIndex != (int)States.StalkingPlayerToKidnap) return;
        LogDebug("Grab target player network event triggered");
        
        SwitchBehaviourStateLocally((int)States.KidnappingPlayer);
    }

    /// <summary>
    /// Resets the required variables and runs setup functions for each particular behaviour state
    /// </summary>
    /// <param name="state">The state to switch to</param>
    private void InitializeState(int state)
    {
        if (!IsServer) return;
        switch (state)
        {
            case (int)States.PassiveRoaming: // 0
            {
                _agentMaxSpeed = 2f;
                _agentMaxAcceleration = 2f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 2f;
                _avoidPlayerCoroutine = null;
                _reachedFavouriteSpotForRoaming = false;

                if (_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, false);
                    _currentlyHasDarkSkin = false;
                }
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, false);
                
                break;
            }

            case (int)States.AvoidingPlayer: // 1
            {
                _agentMaxSpeed = 9f;
                _agentMaxAcceleration = 100f;
                _avoidPlayerTimer = -1f;
                _finishedSpottedAnimation = false;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                openDoorSpeedMultiplier = 10f;
                _avoidPlayerAudioTimer = 0f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                
                netcodeController.SetTriggerClientRpc(_aloeId, AloeClient.Spotted);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                
                break;
            }

            case (int)States.PassivelyStalkingPlayer: // 2
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 70f;
                _isStaringAtTargetPlayer = false;
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
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, true);
                
                break; 
            }

            case (int)States.StalkingPlayerToKidnap: // 3
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                _inGrabAnimation = false;
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
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, true);
                
                break;
            }

            case (int)States.KidnappingPlayer: // 4
            {
                _agentMaxSpeed = 8f;
                _agentMaxAcceleration = 20f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _hasTransitionedToRunningForwardsAndCarryingPlayer = false;
                _avoidPlayerTimer = 0f;
                _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;
                openDoorSpeedMultiplier = 10f;
                _avoidPlayerCoroutine = null;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _ignoredNodes.Clear();
                
                // Set target player and pickup player
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, false);
                netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.playerClientId); // Todo: Make function that only does the rpc if needed
                SetTargetPlayerInCaptivity(true);
                netcodeController.IncreasePlayerFearLevelClientRpc(_aloeId, 2.5f, targetPlayer.actualClientId);
                netcodeController.SetTargetPlayerAbleToEscapeClientRpc(_aloeId, false);
                netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.SnatchAndDrag, false);
                
                targetNode = favoriteSpot;
                SetDestinationToPosition(targetNode.position);
                
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                break;
            }

            case (int)States.HealingPlayer: // 5
            {
                agent.speed = 0;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 4f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;

                netcodeController.SetTargetPlayerAbleToEscapeClientRpc(_aloeId, true);
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
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, true);
                netcodeController.ChangeTargetPlayerClientRpc(_aloeId, targetPlayer.actualClientId);
                    
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

            case (int)States.CuddlingPlayer: // 6
            {
                agent.speed = 0;
                _agentMaxSpeed = 0f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
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
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, true);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                
                break;
            }

            case (int)States.ChasingEscapedPlayer: // 7
            {
                _agentMaxSpeed = 6f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
                movingTowardsTargetPlayer = false;
                _inGrabAnimation = false;
                _avoidPlayerTimer = 0f;
                openDoorSpeedMultiplier = 2f;
                _waitTimer = 2f;
                
                if (roamMap.inProgress) StopSearch(roamMap);
                _avoidPlayerCoroutine = null;
                
                netcodeController.PlayAudioClipTypeServerRpc(_aloeId, AloeClient.AudioClipTypes.Chase, false);
                if (!_currentlyHasDarkSkin)
                {
                    netcodeController.ChangeAloeSkinColourClientRpc(_aloeId, true);
                    _currentlyHasDarkSkin = true;
                }
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Healing, false);
                
                break;
            }

            case (int)States.AttackingPlayer: // 8
            {
                _agentMaxSpeed = 5f;
                _agentMaxAcceleration = 50f;
                _isStaringAtTargetPlayer = false;
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
                
                netcodeController.ChangeAnimationParameterBoolClientRpc(_aloeId, AloeClient.Crawling, false);
                
                break;
            }
            
            case (int)States.Dead: // 9
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
                
                SetTargetPlayerInCaptivity(false);

                break;
            }
        }
    }

    /// <summary>
    /// Switches to the given behaviour state represented by the state enum
    /// </summary>
    /// <param name="state">The state enum to change to</param>
    private void SwitchBehaviourStateLocally(States state)
    {
        SwitchBehaviourStateLocally((int)state);
    }

    /// <summary>
    /// Switches to the given behaviour state represented by an integer
    /// </summary>
    /// <param name="state">The state integer to change to</param>
    private void SwitchBehaviourStateLocally(int state)
    {
        if (!IsServer || currentBehaviourStateIndex == state) return;
        LogDebug($"Switched to behaviour state {state}!");
        previousBehaviourStateIndex = currentBehaviourStateIndex;
        currentBehaviourStateIndex = state;
        InitializeState(state);
        netcodeController.ChangeBehaviourStateClientRpc(_aloeId, state);
        LogDebug($"Switch to behaviour state {state} complete!");
    }

    /// <summary>
    /// Gets the max health of the given player
    /// This is needed because mods may increase the max health of a player
    /// </summary>
    /// <param name="player">The player to get the max health</param>
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
        
        if (stunNormalizedTimer > 0 || _isStaringAtTargetPlayer)
        {
            agent.speed = 0;
            agent.acceleration = _agentMaxAcceleration;
        }

        else if (currentBehaviourStateIndex != (int)States.Dead)
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
    /// <param name="msg">The debug message to log</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo($"State:{currentBehaviourStateIndex}, {msg}");
        #endif
    }
}