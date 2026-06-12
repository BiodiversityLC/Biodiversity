using Biodiversity.Creatures.Core.StateMachine;
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

    public CachedList<GameObject> CachedInsideAINodes;
    public CachedList<GameObject> CachedOutsideAINodes;

    public override void Awake()
    {
        base.Awake();
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
        Unknown
    }

    /// <summary>
    /// Finds a position near <paramref name="beingDroppedFrom"/> that is on the navmesh and has a complete
    /// navmesh path from <paramref name="reachableAnchor"/>, filtering out positions in pits or rooms
    /// that are disconnected from the playable area (e.g., by locked doors).
    /// Tries <paramref name="beingDroppedFrom"/> itself first, then candidate positions in concentric rings
    /// of increasing radius around it.
    /// </summary>
    /// <param name="dropPosition">The resulting on-navmesh, reachable position. <c>default</c> if none was found.</param>
    /// <param name="beingDroppedFrom">The position to search around (e.g. the monster's position).</param>
    /// <param name="reachableAnchor">A position known to be reachable by players (e.g. the main entrance). Snapped onto the navmesh before pathing; if it cannot be snapped within <paramref name="sampleRadius"/>, the search fails.</param>
    /// <param name="maxDropRadius">The radius of the outermost candidate ring.</param>
    /// <param name="sampleRadius">The maximum distance a raw candidate may be snapped onto the navmesh.</param>
    /// <param name="ringCount">The number of concentric candidate rings around <paramref name="beingDroppedFrom"/>.</param>
    /// <param name="candidatePositionsPerRing">The number of evenly spaced candidate positions per ring.</param>
    /// <returns><c>true</c> if a reachable drop position was found; otherwise, <c>false</c>.</returns>
    internal static bool TryGetReachableDropPosition(
        out Vector3 dropPosition,
        Vector3 beingDroppedFrom,
        Vector3 reachableAnchor,
        float maxDropRadius = 10f,
        float sampleRadius = 4f,
        int ringCount = 3,
        int candidatePositionsPerRing = 8)
    {
        dropPosition = default;

        // Snap the anchor onto the navmesh
        if (!NavMesh.SamplePosition(reachableAnchor, out NavMeshHit anchorHit, sampleRadius, NavMesh.AllAreas))
            return false; // Anchor itself is off the mesh

        NavMeshPath path = new();

        // Best case: directly on the position the player is being dropped from.
        if (IsCandidateReachable(beingDroppedFrom, anchorHit.position, sampleRadius, path, out dropPosition))
            return true;

        // Otherwise spiral outward in rings.
        for (int ring = 1; ring <= ringCount; ring++)
        {
            float radius = maxDropRadius * ring / ringCount;

            for (int i = 0; i < candidatePositionsPerRing; i++)
            {
                float angle = 360f / candidatePositionsPerRing * i * Mathf.Deg2Rad;
                Vector3 candidate = beingDroppedFrom
                                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (IsCandidateReachable(candidate, anchorHit.position, sampleRadius, path, out dropPosition))
                    return true;
            }
        }

        return false;

        static bool IsCandidateReachable(
            Vector3 candidate,
            Vector3 anchorOnMesh,
            float sampleRadius,
            NavMeshPath path,
            out Vector3 dropPosition)
        {
            dropPosition = default;

            // Snap the raw candidate (may be in the air / inside a wall) onto the mesh.
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
                return false;

            // Check if the path is valid
            if (!NavMesh.CalculatePath(anchorOnMesh, hit.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return false;

            dropPosition = hit.position;
            return true;
        }
    }

    /// <summary>
    /// Checks if a valid path exists from <paramref name="startPosition"/> to <paramref name="targetPosition"/>.
    /// </summary>
    /// <param name="startPosition">The starting position of the path. Snapped onto the nearest navmesh surface within 2 units; if it cannot be snapped, the path is <see cref="PathStatus.Invalid"/>.</param>
    /// <param name="targetPosition">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="nearEnoughDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <param name="areaMask">The navmesh area mask used for sampling and path calculation.</param>
    /// <returns>
    /// <see cref="PathStatus.Valid"/> if the distance between <paramref name="startPosition"/> and <paramref name="targetPosition"/>
    /// is at most <paramref name="nearEnoughDistance"/>, or if a complete path exists;
    /// <see cref="PathStatus.ValidButInLos"/> if <paramref name="checkLineOfSight"/> is <c>true</c> and a complete path exists
    /// but is obstructed by line of sight or visible to a living player;
    /// otherwise, <see cref="PathStatus.Invalid"/>.
    /// </returns>
    internal static PathStatus IsPathValid(
        Vector3 startPosition,
        Vector3 targetPosition,
        bool checkLineOfSight = false,
        float nearEnoughDistance = 0f,
        int areaMask = NavMesh.AllAreas)
    {
        // Check if the target position is within the buffer distance
        if (Vector3.Distance(startPosition, targetPosition) <= nearEnoughDistance)
            return PathStatus.Valid;

        // Try to snap startPosition onto the nearest valid NavMesh surface within 2 units
        if (!NavMesh.SamplePosition(startPosition, out NavMeshHit startNavMeshHit, 2f, areaMask))
            return PathStatus.Invalid;

        NavMeshPath path = new();

        // Calculate path to the target position and check if it's complete
        if (!NavMesh.CalculatePath(startNavMeshHit.position, targetPosition, areaMask, path)
            || path.status != NavMeshPathStatus.PathComplete
            || path.corners.Length == 0)
            return PathStatus.Invalid;

        // Check if any segment of the path is intersected by line of sight
        if (checkLineOfSight)
        {
            if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit targetNavMeshHit, 2.7f, areaMask)
                || Vector3.Distance(path.corners[^1], targetNavMeshHit.position) > 1.5f)
                return PathStatus.ValidButInLos;

            for (int i = 1; i < path.corners.Length; ++i)
            {
                if (Physics.Linecast(path.corners[i - 1], path.corners[i], 262144))
                    return PathStatus.ValidButInLos;
            }

            PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;
            for (int i = 0; i < allPlayers.Length; i++)
            {
                PlayerControllerB player = allPlayers[i];
                if (PlayerUtil.IsPlayerDead(player)) continue;
                if (player.HasLineOfSightToPosition(targetPosition, 70f, 80, 1))
                    return PathStatus.ValidButInLos;
            }
        }

        return PathStatus.Valid;
    }

    /// <summary>
    /// Checks if the <paramref name="agent"/> can construct a valid path to <paramref name="targetPosition"/>.
    /// </summary>
    /// <param name="agent">The <see cref="NavMeshAgent"/> to construct the path for.</param>
    /// <param name="targetPosition">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="nearEnoughDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <returns>
    /// <see cref="PathStatus.Invalid"/> if the agent is not on a navmesh; otherwise, the result of <see cref="IsPathValid"/>.
    /// </returns>
    internal static PathStatus IsAgentPathValid(
        NavMeshAgent agent,
        Vector3 targetPosition,
        bool checkLineOfSight = false,
        float nearEnoughDistance = 0f)
    {
        if (!agent.isOnNavMesh) return PathStatus.Invalid;

        return IsPathValid(agent.transform.position, targetPosition, checkLineOfSight, nearEnoughDistance, agent.areaMask);
    }

    /// <summary>
    /// Gets the closest valid AI node to <paramref name="referencePosition"/> that can be pathed to from <paramref name="startPosition"/>.
    /// </summary>
    /// <param name="pathStatus">The <see cref="PathStatus"/> of the path to the returned node. <see cref="PathStatus.Invalid"/> if no node was found.</param>
    /// <param name="startPosition">The position in which paths are calculated from (e.g. the entity that will travel to the node).</param>
    /// <param name="referencePosition">The position node distances are measured from. Not a path endpoint; the nodes themselves are the path targets.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If <c>true</c>, a node whose path is blocked by line of sight may be returned when no fully unobstructed node exists.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from <paramref name="referencePosition"/> to be considered.</param>
    /// <param name="areaMask">The navmesh area mask used for sampling and path calculation.</param>
    /// <returns>The transform of the closest valid AI node reachable from <paramref name="startPosition"/>, or null if no valid node is found.</returns>
    internal static Transform GetClosestValidNodeToPosition(
        out PathStatus pathStatus,
        Vector3 startPosition,
        Vector3 referencePosition,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f,
        int areaMask = NavMesh.AllAreas)
    {
        return GetValidNodeFromPosition(
            findClosest: true,
            pathStatus: out pathStatus,
            startPosition: startPosition,
            referencePosition: referencePosition,
            givenAiNodes: givenAiNodes,
            ignoredAINodes: ignoredAINodes,
            checkLineOfSight: checkLineOfSight,
            allowFallbackIfBlocked: allowFallbackIfBlocked,
            bufferDistance: bufferDistance,
            areaMask: areaMask);
    }

    /// <summary>
    /// Gets the AI node farthest from <paramref name="referencePosition"/> that can be pathed to from <paramref name="startPosition"/>.
    /// </summary>
    /// <param name="pathStatus">The <see cref="PathStatus"/> of the path to the returned node. <see cref="PathStatus.Invalid"/> if no node was found.</param>
    /// <param name="startPosition">The position in which paths are calculated from (e.g. the entity that will travel to the node).</param>
    /// <param name="referencePosition">The position node distances are measured from. Not a path endpoint; the nodes themselves are the path targets.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If <c>true</c>, a node whose path is blocked by line of sight may be returned when no fully unobstructed node exists.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from <paramref name="referencePosition"/> to be considered.</param>
    /// <param name="areaMask">The navmesh area mask used for sampling and path calculation.</param>
    /// <returns>The transform of the farthest valid AI node reachable from <paramref name="startPosition"/>, or null if no valid node is found.</returns>
    internal static Transform GetFarthestValidNodeFromPosition(
        out PathStatus pathStatus,
        Vector3 startPosition,
        Vector3 referencePosition,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f,
        int areaMask = NavMesh.AllAreas)
    {
        return GetValidNodeFromPosition(
            findClosest: false,
            pathStatus: out pathStatus,
            startPosition: startPosition,
            referencePosition: referencePosition,
            givenAiNodes: givenAiNodes,
            ignoredAINodes: ignoredAINodes,
            checkLineOfSight: checkLineOfSight,
            allowFallbackIfBlocked: allowFallbackIfBlocked,
            bufferDistance: bufferDistance,
            areaMask: areaMask);
    }

    /// <summary>
    /// Gets the closest or farthest AI node relative to <paramref name="referencePosition"/> that can be pathed to
    /// from <paramref name="startPosition"/>. Candidates are sorted by distance to <paramref name="referencePosition"/>
    /// and the first with a fully valid path is returned; if <paramref name="allowFallbackIfBlocked"/> is <c>true</c>, the best
    /// line-of-sight-blocked node is kept as a fallback.
    /// </summary>
    /// <param name="findClosest">Whether to find the closest valid node (<c>true</c>) or the farthest valid node (<c>false</c>).</param>
    /// <param name="pathStatus">The <see cref="PathStatus"/> of the path to the returned node. <see cref="PathStatus.Invalid"/> if no node was found.</param>
    /// <param name="startPosition">The position paths are calculated from (e.g. the entity that will travel to the node).</param>
    /// <param name="referencePosition">The position node distances are measured from. Not a path endpoint; the nodes themselves are the path targets.</param>
    /// <param name="givenAiNodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If <c>true</c>, a node whose path is blocked by line of sight may be returned when no fully unobstructed node exists.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from <paramref name="referencePosition"/> to be considered.</param>
    /// <param name="areaMask">The navmesh area mask used for sampling and path calculation.</param>
    /// <returns>The transform of the valid AI node reachable from <paramref name="startPosition"/>, or null if no valid node is found.</returns>
    private static Transform GetValidNodeFromPosition(
        bool findClosest,
        out PathStatus pathStatus,
        Vector3 startPosition,
        Vector3 referencePosition,
        IEnumerable<GameObject> givenAiNodes,
        List<GameObject> ignoredAINodes,
        bool checkLineOfSight,
        bool allowFallbackIfBlocked,
        float bufferDistance,
        int areaMask)
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
            if (Vector3.Distance(referencePosition, node.transform.position) <= bufferDistance) continue;
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
            float squareDistanceA = (a.transform.position - referencePosition).sqrMagnitude;
            float squareDistanceB = (b.transform.position - referencePosition).sqrMagnitude;
            return findClosest ? squareDistanceA.CompareTo(squareDistanceB) : squareDistanceB.CompareTo(squareDistanceA);
        });

        Transform bestNode = null;
        pathStatus = PathStatus.Invalid;

        try
        {
            for (int i = 0; i < candidateNodes.Count; i++)
            {
                GameObject node = candidateNodes[i];
                PathStatus currentPathStatus = IsPathValid(startPosition, node.transform.position, checkLineOfSight: checkLineOfSight, areaMask: areaMask);

                if (currentPathStatus == PathStatus.Valid)
                {
                    pathStatus = PathStatus.Valid;
                    bestNode = node.transform;
                    break;
                }

                if (currentPathStatus == PathStatus.ValidButInLos && allowFallbackIfBlocked)
                {
                    if (!bestNode)
                    {
                        pathStatus = PathStatus.ValidButInLos;
                        bestNode = node.transform;
                        // Don't break here, keep searching for a better node and keep this one as a potential fallback
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
    /// </summary>
    /// <param name="targetPosition">The position to check line of sight to.</param>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="proximityAwareness">The proximity awareness range. If the value is less than zero, then it is assumed that there is no proximity awareness at all.</param>
    /// <returns>Returns <c>true</c> if the AI has line of sight to the given position; otherwise, <c>false</c>.</returns>
    internal bool HasLineOfSight(
        Vector3 targetPosition,
        Transform eyeTransform = null,
        float viewWidth = 45f,
        float viewRange = 60f,
        float proximityAwareness = -1f)
    {
        if (!eyeTransform)
        {
            eyeTransform = eye;
        }

        bool isFoggy = isOutside && !enemyType.canSeeThroughFog &&
                       TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy;

        return LineOfSightUtil.HasLineOfSight(targetPosition, eyeTransform, viewWidth, viewRange, proximityAwareness,
            isFoggy);
    }

    internal bool IsAPlayerInLineOfSightToEye(
        Transform eyeTransform,
        float width = 45f,
        float range = 60f)
    {
        PlayerControllerB[] allPlayers = StartOfRound.Instance.allPlayerScripts;
        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerControllerB player = allPlayers[i];

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
    /// <param name="proximityAwareness"></param>
    /// <returns>The "best" (according to the parameters) player target, or null if none are found.</returns>
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
    /// Finds the closest visible item.
    /// </summary>
    /// <param name="possibleTargets">The list of all possible items that could be the closest.</param>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="onlyInside">True for only searching items inside the facility.</param>
    /// <param name="onlyOutside">True for only searching items outside the facility.</param>
    /// <param name="ignoreInShip">True to ignore items located inside the ship.</param>
    /// <param name="ignoreHeld">True to ignore items that are held by players or by other enemies.</param>
    /// <returns>The closest valid item or null if no items is found.</returns>
    internal GrabbableObject GetClosestVisibleItem(
        List<GrabbableObject> possibleTargets,
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        bool onlyInside = false,
        bool onlyOutside = false,
        bool ignoreInShip = false,
        bool ignoreHeld = false)
    {
        for (int i = 0; i < possibleTargets.Count; i++)
        {
            GrabbableObject target = possibleTargets[i];
            if (target == null || (onlyInside && !target.isInFactory) || (onlyOutside && target.isInFactory) || (ignoreInShip && target.isInShipRoom) || (ignoreHeld && (target.isHeld || target.isHeldByEnemy)))
            {
                continue;
            }
            if (HasLineOfSight(target.transform.position, eyeTransform, viewWidth, viewRange))
            {
                return target;
            }
        }
        return null;
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
    /// Detects whether the player is reachable by the <see cref="NavMeshAgent"/> via a path, and targetable according to <see cref="PlayerTargetableConditions"/>.
    /// </summary>
    /// <param name="player">The target player to check for reachability.</param>
    /// <returns>Returns <c>true</c> if the player is reachable by the AI; otherwise, <c>false</c>.</returns>
    internal bool IsPlayerReachableAndTargetable(
        PlayerControllerB player)
    {
        // 1). Check if the player is targetable
        if (!PlayerTargetableConditions.IsPlayerTargetable(player))
        {
            return false;
        }

        // 2). Check if we can draw a valid path to the player
        if (IsAgentPathValid(agent, player.transform.position) == PathStatus.Invalid)
        {
            return false;
        }

        // A valid path exists
        return true;
    }

    #region Distance Calculators
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
    #endregion

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