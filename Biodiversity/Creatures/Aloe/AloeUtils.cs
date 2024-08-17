using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Biodiversity.Creatures.Aloe;

public static class AloeUtils
{
    public enum PathStatus
    {
        Invalid, // Path is invalid or incomplete
        ValidButInLos, // Path is valid but obstructed by line of sight
        Valid, // Path is valid and unobstructed
        Unknown,
    }
    
    /// <summary>
    /// Checks if the AI can construct a valid path to the given position.
    /// </summary>
    /// <param name="agent">The NavMeshAgent to construct the path for.</param>
    /// <param name="position">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="bufferDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns true if the agent can path to the position within the buffer distance or if a valid path exists; otherwise, false.</returns>
    public static PathStatus IsPathValid(
        NavMeshAgent agent, 
        Vector3 position, 
        bool checkLineOfSight = false, 
        float bufferDistance = 0f, 
        ManualLogSource logSource = null)
    {
        // Check if the desired location is within the buffer distance
        if (Vector3.Distance(agent.transform.position, position) <= bufferDistance)
        {
            //LogDebug(logSource, $"Target position {position} is within buffer distance {bufferDistance}.");
            return PathStatus.Valid;
        }
        
        NavMeshPath path = new();

        // Calculate path to the target position
        if (!agent.CalculatePath(position, path) || path.corners.Length == 0)
        {
            return PathStatus.Invalid;
        }

        // Check if the path is complete
        if (path.status != NavMeshPathStatus.PathComplete)
        {
            return PathStatus.Invalid;
        }

        // Check if any segment of the path is intersected by line of sight
        if (checkLineOfSight)
        {
            if (Vector3.Distance(path.corners[^1], RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5)
                return PathStatus.ValidButInLos;
            
            for (int i = 1; i < path.corners.Length; ++i)
            {
                if (Physics.Linecast(path.corners[i - 1], path.corners[i], 262144))
                {
                    return PathStatus.ValidButInLos;
                }
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
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The transform of the closest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    public static Transform GetClosestValidNodeToPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position, 
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f,
        ManualLogSource logSource = null)
    {
        return GetValidNodeFromPosition(
            findClosest: true, 
            pathStatus:out pathStatus, 
            agent: agent, 
            position: position, 
            allAINodes: allAINodes, 
            ignoredAINodes: ignoredAINodes, 
            checkLineOfSight: checkLineOfSight, 
            allowFallbackIfBlocked: allowFallbackIfBlocked, 
            bufferDistance: bufferDistance, 
            logSource: logSource);
    }

    /// <summary>
    /// Gets the farthest valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The transform of the farthest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    public static Transform GetFarthestValidNodeFromPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f,
        ManualLogSource logSource = null)
    {
        return GetValidNodeFromPosition(
            findClosest: false, 
            pathStatus:out pathStatus, 
            agent: agent, 
            position: position, 
            allAINodes: allAINodes, 
            ignoredAINodes: ignoredAINodes, 
            checkLineOfSight: checkLineOfSight, 
            allowFallbackIfBlocked: allowFallbackIfBlocked, 
            bufferDistance: bufferDistance, 
            logSource: logSource);
    }
    
    /// <summary>
    /// Gets a valid AI node from the specified position that the NavMeshAgent can path to.
    /// </summary>
    /// <param name="findClosest">Whether to find the closest valid node (true) or the farthest valid node (false).</param>
    /// <param name="pathStatus">The PathStatus enum indicating the validity of the path.</param>
    /// <param name="agent">The NavMeshAgent to calculate the path for.</param>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The transform of the valid AI node that the agent can path to, or null if no valid node is found.</returns>
    private static Transform GetValidNodeFromPosition(
        bool findClosest,
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes,
        bool checkLineOfSight,
        bool allowFallbackIfBlocked,
        float bufferDistance,
        ManualLogSource logSource
        )
    {
        HashSet<GameObject> ignoredNodesSet = ignoredAINodes == null ? [] : [..ignoredAINodes];
        
        List<GameObject> aiNodes = allAINodes
            .Where(node => !ignoredNodesSet.Contains(node) && Vector3.Distance(position, node.transform.position) > bufferDistance)
            .ToList();
        
        aiNodes.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(position, a.transform.position);
            float distanceB = Vector3.Distance(position, b.transform.position);
            return findClosest ? distanceA.CompareTo(distanceB) : distanceB.CompareTo(distanceA);
        });

        foreach (GameObject node in aiNodes)
        {
            pathStatus = IsPathValid(agent, node.transform.position, checkLineOfSight, logSource: logSource);
            if (pathStatus == PathStatus.Valid)
            {
                return node.transform;
            }

            if (pathStatus == PathStatus.ValidButInLos && allowFallbackIfBlocked)
            {
                // Try to find another valid node without checking line of sight
                foreach (GameObject fallbackNode in aiNodes)
                {
                    if (fallbackNode == node) continue;
                    PathStatus fallbackStatus = IsPathValid(
                        agent, 
                        fallbackNode.transform.position,
                        logSource: logSource);

                    if (fallbackStatus == PathStatus.Valid)
                    {
                        pathStatus = PathStatus.ValidButInLos;
                        return fallbackNode.transform;
                    }
                }
            }
        }

        pathStatus = PathStatus.Invalid;
        return null;
    }

    /// <summary>
    /// Determines whether the AI has line of sight to the given position.
    /// </summary>
    /// <param name="pos">The position to check for line of sight.</param>
    /// <param name="eye">The eye transform of the AI.</param>
    /// <param name="width">The AI's view width in degrees.</param>
    /// <param name="range">The AI's view range in units.</param>
    /// <param name="inputProximityAwareness">The proximity awareness range of the AI.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns true if the AI has line of sight to the given position; otherwise, false.</returns>
    public static bool DoesEyeHaveLineOfSightToPosition(
        Vector3 pos,
        Transform eye,
        float width = 45f,
        int range = 60,
        float inputProximityAwareness = -1f,
        ManualLogSource logSource = null)
    {
        return Vector3.Distance(eye.position, pos) < range && !Physics.Linecast(eye.position, pos, StartOfRound.Instance.collidersAndRoomMaskAndDefault) && (Vector3.Angle(eye.forward, pos - eye.position) < width || Vector3.Distance(eye.position, pos) < inputProximityAwareness);
    }

    /// <summary>
    /// Determines the closest player that the eye can see, considering a buffer distance to avoid constant target switching.
    /// </summary>
    /// <param name="eye">The transform representing the eye position and direction.</param>
    /// <param name="width">The view width of the eye in degrees.</param>
    /// <param name="range">The view range of the eye in units.</param>
    /// <param name="currentVisiblePlayer">The currently visible player to compare distances against.</param>
    /// <param name="bufferDistance">The buffer distance to prevent constant target switching.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns the closest visible player to the eye, or null if no player is found.</returns>
    public static PlayerControllerB GetClosestVisiblePlayerFromEye(
        Transform eye,
        float width = 45f,
        int range = 60,
        PlayerControllerB currentVisiblePlayer = null,
        float bufferDistance = 1.5f,
        ManualLogSource logSource = null)
    {
        PlayerControllerB closestPlayer = null;
        float closestDistance = float.MaxValue;
        
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (!IsPlayerTargetable(player)) continue;
            if (player == currentVisiblePlayer) continue;

            if (!DoesEyeHaveLineOfSightToPosition(player.transform.position, eye, width, range, logSource: logSource)) continue;
            float distance = Vector3.Distance(eye.position, player.transform.position);
            if (!(distance < closestDistance)) continue;

            closestPlayer = player;
            closestDistance = distance;
        }
        
        // If the current visible player is still within the buffer distance, continue targeting it
        if (currentVisiblePlayer != null)
        {
            float currentTargetDistance = Vector3.Distance(eye.position, currentVisiblePlayer.transform.position);
            if (Mathf.Abs(closestDistance - currentTargetDistance) < bufferDistance)
            {
                //LogDebug(logSource, $"Current visible player {currentVisiblePlayer.name} remains within buffer distance");
                return currentVisiblePlayer;
            }
        }

        //LogDebug(logSource, closestPlayer != null ? $"New closest player: {closestPlayer.name}" : "No visible player found");
        return closestPlayer;
    }
    
    /// <summary>
    /// Returns a list of all players that are visible from the eye.
    /// </summary>
    /// <param name="eye">The transform representing the eye position and direction.</param>
    /// <param name="width">The view width of the eye in degrees.</param>
    /// <param name="range">The view range of the eye in units.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns a list of players that are visible from the eye.</returns>
    public static List<PlayerControllerB> GetAllVisiblePlayersFromEye(
        Transform eye,
        float width = 45f,
        int range = 60,
        ManualLogSource logSource = null)
    {
        List<PlayerControllerB> visiblePlayers = [];
        visiblePlayers.AddRange(StartOfRound.Instance.allPlayerScripts.Where(IsPlayerTargetable).Where(player => DoesEyeHaveLineOfSightToPosition(player.transform.position, eye, width, range, logSource: logSource)));

        //LogDebug(logSource, $"Number of visible players: {visiblePlayers.Count}");
        return visiblePlayers;
    }

    /// <summary>
    /// Finds and returns the player that is closest to the specified transform, considering a buffer distance.
    /// </summary>
    /// <param name="players">The list of players to search through.</param>
    /// <param name="transform">The transform to measure distances from.</param>
    /// <param name="inputPlayer">The current player being targeted.</param>
    /// <param name="bufferDistance">The buffer distance to prevent constant target switching.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>The player that is closest to the specified transform within the buffer distance, or the closest player if none are within the buffer distance.</returns>
    public static PlayerControllerB GetClosestPlayerFromList(
        List<PlayerControllerB> players, 
        Transform transform,
        PlayerControllerB inputPlayer, 
        float bufferDistance = 1.5f,
        ManualLogSource logSource = null)
    {
        PlayerControllerB closestPlayer = inputPlayer;
        float closestDistance = float.MaxValue;

        foreach (PlayerControllerB player in players)
        {
            if (player == inputPlayer) continue; // Skip the input player itself
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (!(distance < closestDistance)) continue;
            closestDistance = distance;
            closestPlayer = player;
        }

        if (inputPlayer == null) return closestPlayer;
        float inputPlayerDistance = Vector3.Distance(transform.position, inputPlayer.transform.position);
        return Mathf.Abs(closestDistance - inputPlayerDistance) < bufferDistance ? inputPlayer : closestPlayer;
    }
    
    /// <summary>
    /// Detects whether the player is reachable by the AI via a path.
    /// </summary>
    /// <param name="agent">The NavMeshAgent to see if it can reach the player.</param>
    /// <param name="player">The target player to check for reachability.</param>
    /// <param name="transform">The transform of the AI.</param>
    /// <param name="eye">The eye transform of the AI for line of sight calculations.</param>
    /// <param name="viewWidth">The view width of the AI's field of view in degrees.</param>
    /// <param name="viewRange">The view range of the AI in units.</param>
    /// <param name="bufferDistance">The buffer distance within which the player is considered reachable without further checks.</param>
    /// <param name="requireLineOfSight">Indicates whether a clear line of sight to the player is required.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns true if the player is reachable by the AI; otherwise, false.</returns>
    public static bool IsPlayerReachable(
        NavMeshAgent agent,
        PlayerControllerB player,
        Transform transform,
        Transform eye,
        float viewWidth = 45f,
        int viewRange = 60,
        float bufferDistance = 1.5f,
        bool requireLineOfSight = false,
        ManualLogSource logSource = null)
    {
        if (player == null)
        {
            LogDebug(logSource, "Player is not reachable because the player object is null.");
            return false;
        }
        
        float currentDistance = Vector3.Distance(transform.position, player.transform.position);
        float optimalDistance = currentDistance;
        
        if (IsPlayerTargetable(player) && 
            IsPathValid(agent, player.transform.position, logSource: logSource) == PathStatus.Valid && 
            (!requireLineOfSight || DoesEyeHaveLineOfSightToPosition(player.gameplayCamera.transform.position, eye, viewWidth, viewRange, logSource: logSource)))
        {
            if (currentDistance < optimalDistance)
                optimalDistance = currentDistance;
        }
        
        bool isReachable = Mathf.Abs(optimalDistance - currentDistance) < bufferDistance;

        LogDebug(logSource, $"Is player reachable: {isReachable}");
        return isReachable;
    }

    /// <summary>
    /// Determines which player, if any, is looking at the specified position.
    /// </summary>
    /// <param name="transform">The transform representing the position to check if a player is looking at.</param>
    /// <param name="ignorePlayer">An optional player to exclude from the check.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>Returns the player object that is looking at the specified position, or null if no player is found.</returns>
    public static PlayerControllerB GetClosestPlayerLookingAtPosition(Transform transform, Object ignorePlayer = null, ManualLogSource logSource = null)
    {
        PlayerControllerB closestPlayer = null;
        float closestDistance = float.MaxValue;
        bool isThereAPlayerToIgnore = ignorePlayer != null;

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if (player.isPlayerDead || !player.isInsideFactory) continue;
            if (isThereAPlayerToIgnore && ignorePlayer == player) continue;
            
            if (!player.HasLineOfSightToPosition(transform.position, 60f)) continue;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (!(distance < closestDistance)) continue;
            
            closestPlayer = player;
            closestDistance = distance;
        }

        return closestPlayer;
    }
    
    /// <summary>
    /// Returns a list of all the players who are currently looking at the specified position.
    /// </summary>
    /// <param name="transform">The transform representing the position to check if players are looking at.</param>
    /// <param name="ignorePlayer">An optional player to exclude from the check.</param>
    /// <param name="playerViewWidth">The view width of the players in degrees.</param>
    /// <param name="playerViewRange">The view range of the players in units.</param>
    /// <param name="logSource">The logger to use for debug logs, can be null.</param>
    /// <returns>A list of players who are looking at the specified position.</returns>
    public static List<PlayerControllerB> GetAllPlayersLookingAtPosition(
        Transform transform, 
        Object ignorePlayer = null,
        float playerViewWidth = 45f,
        int playerViewRange = 60,
        ManualLogSource logSource = null)
    {
        List<PlayerControllerB> players = [];
        bool isThereAPlayerToIgnore = ignorePlayer != null;

        players.AddRange(from player in StartOfRound.Instance.allPlayerScripts where !IsPlayerDead(player) where !isThereAPlayerToIgnore || player != ignorePlayer where player.HasLineOfSightToPosition(transform.position, playerViewWidth, playerViewRange) select player);

        return players;
    }
    
    /// <summary>
    /// Determines whether a player is targetable.
    /// This function also takes into account whether the player is being kidnapped by the Aloe, which is the difference between this function and Zeeker's function.
    /// </summary>
    /// <param name="player">The player to check for targetability.</param>
    /// <returns>Returns true if the player is targetable; otherwise, false.</returns>
    public static bool IsPlayerTargetable(PlayerControllerB player)
    {
        if (player == null) return false;
        return !IsPlayerDead(player) &&
               player.isInsideFactory &&
               !(player.sinkingValue >= 0.7300000190734863) &&
               !AloeSharedData.Instance.IsPlayerKidnapBound(player);
    }
    
    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "OutsideAINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all outside AI nodes.</returns>
    public static IEnumerable<Vector3> FindOutsideAINodePositions()
    {
        GameObject[] outsideAINodes = AloeSharedData.Instance.GetOutsideAINodes();
        Vector3[] outsideNodePositions = new Vector3[outsideAINodes.Length];
                
        for (int i = 0; i < outsideAINodes.Length; i++)
        {
            outsideNodePositions[i] = outsideAINodes[i].transform.position;
        }
        
        return outsideNodePositions;
    }

    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "AINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all inside AI nodes.</returns>
    public static IEnumerable<Vector3> FindInsideAINodePositions()
    {
        GameObject[] insideAINodes = AloeSharedData.Instance.GetInsideAINodes();
        Vector3[] insideNodePositions = new Vector3[insideAINodes.Length];
                
        for (int i = 0; i < insideAINodes.Length; i++)
        {
            insideNodePositions[i] = insideAINodes[i].transform.position;
        }
        
        return insideNodePositions;
    }
    
    /// <summary>
    /// Determines whether the specified player is dead.
    /// </summary>
    /// <param name="player">The player to check.</param>
    /// <returns>Returns true if the player is dead or not controlled; otherwise, false.</returns>
    public static bool IsPlayerDead(PlayerControllerB player)
    {
        if (player == null) return true;
        return player.isPlayerDead || !player.isPlayerControlled;
    }

    public static void ChangeNetworkVar<T>(NetworkVariable<T> networkVariable, T newValue) where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
        {
            networkVariable.Value = newValue;
        }
    }
    
    /// <summary>
    /// Smoothly moves the specified transform to the target position.
    /// </summary>
    /// <param name="transform">The transform to move.</param>
    /// <param name="targetPosition">The target position to move to.</param>
    /// <param name="smoothTime">The time it takes to smooth to the target position.</param>
    /// <param name="velocity">A reference to the current velocity, this is modified by the function.</param>
    public static void SmoothMoveTransformTo(Transform transform, Vector3 targetPosition, float smoothTime, ref Vector3 velocity)
    {
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release.
    /// </summary>
    /// <param name="logSource">The log source instance.</param>
    /// <param name="msg">The debug message to log.</param>
    private static void LogDebug(ManualLogSource logSource, string msg)
    {
        #if DEBUG
        logSource?.LogInfo(msg);
        #endif
    }
}