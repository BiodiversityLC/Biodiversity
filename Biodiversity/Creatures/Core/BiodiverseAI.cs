﻿using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures;

public abstract class BiodiverseAI : EnemyAI
{
    /// <summary>
    /// A unique identifier for the object, stored as a networked fixed-size string.
    /// This ID is generated as a GUID on the server and synchronized to all clients.
    /// </summary>
    private readonly NetworkVariable<FixedString32Bytes> _networkBioId = new();
    
    /// <summary>
    /// Gets the unique identifier (BioId) for this object as a string.
    /// </summary>
    public string BioId => _networkBioId.Value.ToString();
    
    /// <summary>
    /// A constant representing a null or unassigned player ID.
    /// </summary>
    internal const ulong NullPlayerId = 69420;
    
    internal readonly PlayerTargetableConditions PlayerTargetableConditions = new();

    public static CachedList<GameObject> CachedInsideAINodes;
    public static CachedList<GameObject> CachedOutsideAINodes;

    private void Awake()
    {
        CachedInsideAINodes = new CachedList<GameObject>(() => GameObject.FindGameObjectsWithTag("AINode").ToList());
        CachedOutsideAINodes = new CachedList<GameObject>(() => GameObject.FindGameObjectsWithTag("OutsideAINode").ToList());
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _networkBioId.Value = new FixedString32Bytes(Guid.NewGuid().ToString("N").Substring(0, 8));
    }

    public override void Start()
    {
        base.Start();
        Random.InitState(StartOfRound.Instance.randomMapSeed + BioId.GetHashCode() - thisEnemyIndex);
    }

    #region Pathing
    /// <summary>
    /// Represents the status of a path.
    /// </summary>
    internal enum PathStatus
    {
        /// <summary>
        /// Path is invalid or incomplete.
        /// </summary>
        Invalid,

        /// <summary>
        /// Path is valid but obstructed by line of sight.
        /// </summary>
        ValidButInLos,

        /// <summary>
        /// Path is valid and unobstructed.
        /// </summary>
        Valid,

        /// <summary>
        /// Path status is unknown.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// Checks if the AI can construct a valid path to the given position.
    /// </summary>
    /// <param name="agent">The NavMeshAgent to construct the path for.</param>
    /// <param name="targetPosition">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="nearEnoughDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <returns>Returns true if the agent can path to the position within the buffer distance or if a valid path exists; otherwise, false.</returns>
    internal static PathStatus IsPathValid(
        NavMeshAgent agent,
        Vector3 targetPosition,
        bool checkLineOfSight = false,
        float nearEnoughDistance = 0f)
    {
        if (!agent.isOnNavMesh) return PathStatus.Invalid;
        
        // Check if the desired location is within the buffer distance
        if (Vector3.Distance(agent.transform.position, targetPosition) <= nearEnoughDistance)
            return PathStatus.Valid;

        NavMeshPath path = new();

        // Calculate path to the target position and check if it's complete before continuing
        if (!agent.CalculatePath(targetPosition, path) || path.status != NavMeshPathStatus.PathComplete || path.corners.Length == 0)
            return PathStatus.Invalid;

        // Check if any segment of the path is intersected by line of sight
        if (checkLineOfSight)
        {
            if (Vector3.Distance(path.corners[^1],
                    RoundManager.Instance.GetNavMeshPosition(targetPosition, RoundManager.Instance.navHit, 2.7f)) > 1.5)
                return PathStatus.ValidButInLos;

            for (int i = 1; i < path.corners.Length; ++i)
            {
                if (Physics.Linecast(path.corners[i - 1], path.corners[i], 262144))
                {
                    return PathStatus.ValidButInLos;
                }
            }

            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
                if (PlayerUtil.IsPlayerDead(player)) continue;
                if (player.HasLineOfSightToPosition(targetPosition, 70f, 80, 1)) return PathStatus.ValidButInLos;
            }
        }

        return PathStatus.Valid;
    }

    /// <summary>
    /// Gets the closest valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <returns>The transform of the closest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    internal static Transform GetClosestValidNodeToPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f)
    {
        return GetValidNodeFromPosition(
            findClosest: true,
            pathStatus: out pathStatus,
            agent: agent,
            position: position,
            givenAiNodes: givenAiNodes,
            ignoredAINodes: ignoredAINodes,
            checkLineOfSight: checkLineOfSight,
            allowFallbackIfBlocked: allowFallbackIfBlocked,
            bufferDistance: bufferDistance);
    }

    /// <summary>
    /// Gets the farthest valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <returns>The transform of the farthest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    internal static Transform GetFarthestValidNodeFromPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f)
    {
        return GetValidNodeFromPosition(
            findClosest: false,
            pathStatus: out pathStatus,
            agent: agent,
            position: position,
            givenAiNodes: givenAiNodes,
            ignoredAINodes: ignoredAINodes,
            checkLineOfSight: checkLineOfSight,
            allowFallbackIfBlocked: allowFallbackIfBlocked,
            bufferDistance: bufferDistance);
    }

    /// <summary>
    /// Gets a valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="findClosest">Whether to find the closest valid node (true) or the farthest valid node (false).</param>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <returns>The transform of the valid AI node that the agent can path to, or null if no valid node is found.</returns>
    private static Transform GetValidNodeFromPosition(
        bool findClosest,
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes,
        bool checkLineOfSight,
        bool allowFallbackIfBlocked,
        float bufferDistance)
    {
        if (givenAiNodes == null)
        {
            pathStatus = PathStatus.Invalid;
            return null;
        }
        
        HashSet<GameObject> ignoredNodesSet = CollectionPool<HashSet<GameObject>, GameObject>.Get();
        if (ignoredAINodes != null)
        {
            for (int i = 0; i < ignoredAINodes.Count; i++)
            {
                ignoredNodesSet.Add(ignoredAINodes[i]);
            }
        }
        
        List<GameObject> candidateNodes = ListPool<GameObject>.Get();
        foreach (GameObject node in givenAiNodes)
        {
            if (ignoredNodesSet.Contains(node)) continue;
            if (Vector3.Distance(position, node.transform.position) <= bufferDistance) continue;
            candidateNodes.Add(node);
        }
        
        CollectionPool<HashSet<GameObject>, GameObject>.Release(ignoredNodesSet);

        if (candidateNodes.Count == 0)
        {
            ListPool<GameObject>.Release(candidateNodes);
            pathStatus = PathStatus.Invalid;
            return null;
        }

        candidateNodes.Sort((a, b) =>
        {
            float squareDistanceA = (a.transform.position - position).sqrMagnitude;
            float squareDistanceB = (b.transform.position - position).sqrMagnitude;
            return findClosest ? squareDistanceA.CompareTo(squareDistanceB) : squareDistanceB.CompareTo(squareDistanceA);
        });

        Transform bestNode = null;
        pathStatus = PathStatus.Invalid;

        try
        {
            for (int i = 0; i < candidateNodes.Count; i++)
            {
                GameObject node = candidateNodes[i];
                PathStatus currentPathStatus = IsPathValid(agent, node.transform.position, checkLineOfSight);

                if (currentPathStatus == PathStatus.Valid)
                {
                    pathStatus = PathStatus.Valid;
                    bestNode = node.transform;
                    break;
                }

                if (currentPathStatus == PathStatus.ValidButInLos && allowFallbackIfBlocked)
                {
                    if (bestNode == null)
                    {
                        pathStatus = PathStatus.ValidButInLos;
                        bestNode = node.transform;
                        // Dont break here, keep searching for a better node and keep this one as a potential fallback
                    }
                }
            }

            return bestNode;
        }
        finally
        {
            ListPool<GameObject>.Release(candidateNodes);
        }
    }
    #endregion

    #region Line Of Sight Stuff
    /// <summary>
    /// Determines if there is an unobstructed line of sight to a position within a specified view cone and range.
    /// This is a pure geometric and physics check.
    /// </summary>
    /// <param name="targetPosition">The position to check line of sight to.</param>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="proximityAwareness">The proximity awareness range. If the value is less than zero, then it is assumed that there is no proximity awareness at all.</param>
    /// <returns>Returns true if the AI has line of sight to the given position; otherwise, false.</returns>
    internal bool HasLineOfSight(
        Vector3 targetPosition,
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        float proximityAwareness = -1f)
    {
        // LogVerbose($"In {nameof(HasLineOfSight)}");
        Vector3 eyePosition = eyeTransform.position;
        Vector3 directionToTarget = targetPosition - eyePosition;
        float distance = directionToTarget.magnitude;
        
        // 1). Range check
        if (distance > viewRange)
        {
            // LogVerbose($"Distance check failed.");
            return false;
        }
        
        // 2). FOV check
        // The proximity can bypass the FOV check, but not the physics obstruction check
        if (distance > proximityAwareness)
        {
            float dotProduct = Vector3.Dot(eyeTransform.forward, directionToTarget.normalized);
            if (dotProduct < Mathf.Cos(viewWidth * 0.5f * Mathf.Deg2Rad))
            {
                // LogVerbose($"Dot product check failed: {dotProduct} < {Mathf.Cos(viewWidth * 0.5f * Mathf.Deg2Rad)}");
                return false;
            }
        }
        
        // 3). Obstruction check
        if (Physics.Linecast(eyePosition, targetPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
        {
            // LogVerbose("Line of sight check failed");
            return false;
        }

        return true;
    }
    
    internal bool IsAPlayerInLineOfSightToEye(
        Transform eyeTransform,
        float width = 45f,
        float range = 60f)
    {
        for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[i];
            
            if (!PlayerTargetableConditions.IsPlayerTargetable(player)) continue;
            if (HasLineOfSight(player.gameplayCamera.transform.position, eyeTransform, width, range))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the closest visible and targetable player.
    /// Uses a buffer distance to prevent rapid target switching.
    /// </summary>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="currentTargetPlayer">The player currently being targeted.</param>
    /// <param name="bufferDistance">The distance buffer to prevent target switching. A new target must be this much closer to be chosen.</param>
    /// <returns>The best player target, or null if none are found.</returns>
    internal PlayerControllerB GetClosestVisiblePlayer(
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        PlayerControllerB currentTargetPlayer = null,
        float bufferDistance = 1.5f,
        float proximityAwareness = -1f)
    {
        // LogVerbose($"In {nameof(GetClosestVisiblePlayer)}");
        PlayerControllerB bestTarget = null;
        float bestTargetDistanceSqr = float.MaxValue;
        
        PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;
        
        // First, re-validate the current target
        float currentTargetDistanceSqr = float.MaxValue;
        if (currentTargetPlayer && PlayerTargetableConditions.IsPlayerTargetable(currentTargetPlayer))
        {
            if (HasLineOfSight(currentTargetPlayer.gameplayCamera.transform.position, eyeTransform, viewWidth,
                    viewRange, proximityAwareness))
            {
                // The current target player is still valid, and it will be our baseline
                bestTarget = currentTargetPlayer;
                currentTargetDistanceSqr = (currentTargetPlayer.transform.position - eyeTransform.position).sqrMagnitude;
                bestTargetDistanceSqr = currentTargetDistanceSqr;
            }
        }

        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerControllerB potentialTarget = allPlayers[i];
            // LogVerbose($"Evaluating player {potentialTarget.playerUsername}");
            
            // Skip the check if this player is the current target player; they have already been validated
            if (potentialTarget == currentTargetPlayer) continue;
            if (!PlayerTargetableConditions.IsPlayerTargetable(potentialTarget))
            {
                // LogVerbose($"Player {potentialTarget.playerUsername} is not targetable.");
                continue;
            }
            
            Vector3 targetPosition = potentialTarget.gameplayCamera.transform.position;
            if (!HasLineOfSight(targetPosition, eyeTransform, viewWidth, viewRange, proximityAwareness))
            {
                // LogVerbose($"Player {potentialTarget.playerUsername} is not in LOS.");
                continue;
            }
            
            float potentialTargetDistanceSqr = (potentialTarget.transform.position - eyeTransform.position).sqrMagnitude;
            if (potentialTargetDistanceSqr < bestTargetDistanceSqr)
            {
                bestTarget = potentialTarget;
                bestTargetDistanceSqr = potentialTargetDistanceSqr;
            }
        }
        
        // If we switched targets, ensure that the new target is significantly closer
        if (bestTarget && currentTargetPlayer && bestTarget != currentTargetPlayer)
        {
            // If the old target player is still valid and the new one isn't closer by the buffer amount, then revert
            if (bestTargetDistanceSqr > currentTargetDistanceSqr - bufferDistance * bufferDistance)
            {
                return currentTargetPlayer;
            }
        }

        return bestTarget;
    }
    
    /// <summary>
    /// Determines the closest player, if any, is looking at the specified position.
    /// </summary>
    /// <param name="position">The position to check if a player is looking at.</param>
    /// <param name="ignorePlayer">An optional player to exclude from the check.</param>
    /// <returns>Returns the player object that is looking at the specified position, or null if no player is found.</returns>
    internal static PlayerControllerB GetClosestPlayerLookingAtPosition(
        Vector3 position, 
        PlayerControllerB ignorePlayer = null)
    {
        PlayerControllerB closestPlayer = null;
        float closestDistanceSqr = float.MaxValue;

        List<PlayerControllerB> visiblePlayers = GetAllPlayersLookingAtPositionPooled(position, ignorePlayer);

        for (int i = 0; i < visiblePlayers.Count; i++)
        {
            PlayerControllerB player = visiblePlayers[i];
            float distanceSqr = (player.transform.position - position).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestPlayer = player;
            }
        }
        
        ListPool<PlayerControllerB>.Release(visiblePlayers);
        return closestPlayer;
    }
    
    /// <summary>
    /// Returns a list of all the players who are currently looking at the specified position.
    /// </summary>
    /// <param name="position">The position to check if a player is looking at.</param>
    /// <param name="ignorePlayer">An optional player to exclude from the check.</param>
    /// <param name="playerViewWidth">The view width of the players in degrees.</param>
    /// <param name="playerViewRange">The view range of the players in units.</param>
    /// <returns>A list of players who are looking at the specified position.</returns>
    internal static List<PlayerControllerB> GetAllPlayersLookingAtPosition(
        Vector3 position, 
        PlayerControllerB ignorePlayer = null,
        float playerViewWidth = 45f,
        int playerViewRange = 60)
    {
        return GetPlayersLookingAtPositionInternal(
            position: position,
            players: [],
            ignorePlayer: ignorePlayer,
            playerViewWidth: playerViewWidth,
            playerViewRange: playerViewRange);
    }
    
    /// <summary>
    /// Returns a pooled list of all the players who are currently looking at the specified position.
    /// The caller must release the list using <see cref="ListPool{PlayerControllerB}"/> once finished.
    /// </summary>
    /// <param name="position">The position to check if a player is looking at.</param>
    /// <param name="ignorePlayer">An optional player to exclude from the check.</param>
    /// <param name="playerViewWidth">The view width of the players in degrees.</param>
    /// <param name="playerViewRange">The view range of the players in units.</param>
    /// <returns>A <see cref="ListPool{PlayerControllerB}"/> of players who are looking at the specified position.</returns>
    internal static List<PlayerControllerB> GetAllPlayersLookingAtPositionPooled(
        Vector3 position, 
        PlayerControllerB ignorePlayer = null,
        float playerViewWidth = 45f,
        int playerViewRange = 60)
    {
        return GetPlayersLookingAtPositionInternal(
            position: position,
            players: ListPool<PlayerControllerB>.Get(),
            ignorePlayer: ignorePlayer,
            playerViewWidth: playerViewWidth,
            playerViewRange: playerViewRange);
    }
    
    private static List<PlayerControllerB> GetPlayersLookingAtPositionInternal(
        Vector3 position,
        List<PlayerControllerB> players,
        PlayerControllerB ignorePlayer = null,
        float playerViewWidth = 45f,
        int playerViewRange = 60)
    {
        PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;
        players.Clear();
        bool shouldIgnore = ignorePlayer;
        
        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerControllerB player = allPlayers[i];
            
            if (PlayerUtil.IsPlayerDead(player) || (shouldIgnore && ignorePlayer == player)) continue;
            if (player.HasLineOfSightToPosition(position, playerViewWidth, playerViewRange))
                players.Add(player);
        }
        return players;
    }
    #endregion
    
    /// <summary>
    /// Detects whether the player is reachable by the NavMeshAgent via a path.
    /// </summary>
    /// <param name="player">The target player to check for reachability.</param>
    /// <param name="eyeTransform">The transform representing the eye.</param>
    /// <param name="viewWidth">The view width of the AI's field of view in degrees.</param>
    /// <param name="viewRange">The view range of the AI in units.</param>
    /// <param name="bufferDistance">The buffer distance within which the player is considered reachable without further checks.</param>
    /// <param name="requireLineOfSight">Indicates whether a clear line of sight to the player is required.</param>
    /// <returns>Returns true if the player is reachable by the AI; otherwise, false.</returns>
    internal bool IsPlayerReachable(
        PlayerControllerB player,
        Transform eyeTransform,
        float viewWidth = 45f,
        int viewRange = 60,
        float bufferDistance = 1.5f,
        bool requireLineOfSight = false)
    {
        if (PlayerUtil.IsPlayerDead(player))
            return false;
        
        float currentDistance = Vector3.Distance(transform.position, player.transform.position);
        float optimalDistance = currentDistance;
        
        if (PlayerTargetableConditions.IsPlayerTargetable(player) && 
            IsPathValid(agent, player.transform.position) == PathStatus.Valid && 
            (!requireLineOfSight || HasLineOfSight(player.gameplayCamera.transform.position, eyeTransform, viewWidth, viewRange)))
        {
            if (currentDistance < optimalDistance)
                optimalDistance = currentDistance;
        }
        
        bool isReachable = Mathf.Abs(optimalDistance - currentDistance) < bufferDistance;
        return isReachable;
    }
    
    public static float Distance2d(GameObject obj1, GameObject obj2)
    {
        return Distance2d(obj1.transform.position, obj2.transform.position);
    }

    public static float Distance2d(Vector3 pos1, Vector3 pos2)
    {
        float deltaX = pos1.x - pos2.x;
        float deltaZ = pos1.z - pos2.z;
        return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        
        // Its a slightly faster way of doing this:
        // return Mathf.Sqrt(Mathf.Pow(pos1.x - pos2.x, 2f) + Mathf.Pow(pos1.z - pos2.z, 2f));
    }
    
    // 2d squared distance formula (cheaper for comparisons)
    public static float Distance2dSq(GameObject obj1, GameObject obj2)
    {
        float deltaX = obj1.transform.position.x - obj2.transform.position.x;
        float deltaZ = obj1.transform.position.z - obj2.transform.position.z;
        return deltaX * deltaX + deltaZ * deltaZ;
    }

    #region Logging
    internal void LogInfo(object message) => BiodiversityPlugin.Logger?.LogInfo($"{GetLogPrefix()} {message}");

    internal void LogVerbose(object message)
    {
        if (BiodiversityPlugin.Config?.VerboseLoggingEnabled ?? false)
            BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");
    }

    internal void LogDebug(object message) => BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");

    internal void LogError(object message) => BiodiversityPlugin.Logger.LogError($"{GetLogPrefix()} {message}");

    internal void LogWarning(object message) => BiodiversityPlugin.Logger.LogWarning($"{GetLogPrefix()} {message}");

    protected virtual string GetLogPrefix()
    {
        return $"[{enemyType.enemyName}]";
    }
    #endregion
}