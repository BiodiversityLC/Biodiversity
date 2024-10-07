using Biodiversity.Util;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures;

internal abstract class BiodiverseAI : EnemyAI
{
    public readonly string BioId = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the mapping between audio type identifiers and their corresponding arrays of <see cref="AudioClip"/>s.
    /// Derived classes must override this property to provide their specific audio clip configurations.
    /// </summary>
    protected Dictionary<string, AudioClip[]> AudioClips { get; } = new();

    /// <summary>
    /// Gets the mapping between audio source identifiers and their corresponding <see cref="AudioSource"/> components.
    /// Derived classes must override this property to provide their specific audio source configurations.
    /// </summary>
    protected Dictionary<string, AudioSource> AudioSources { get; } = new();

    protected virtual void Awake()
    {
        if (!IsServer) return;
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        Random.InitState(StartOfRound.Instance.randomMapSeed + BioId.GetHashCode() - thisEnemyIndex);
    }

    /// <summary>
    /// Requests the server to play a specific type of audio clip on a designated <see cref="AudioSource"/>.
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
    [ServerRpc]
    internal void PlayAudioClipTypeServerRpc(
        string audioClipType,
        string audioSourceType,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false)
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
            audibleByEnemies);
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
    /// (e.g., "Voice", "Footsteps", "Alert").
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
    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(
        string audioClipType,
        string audioSourceType,
        int clipIndex,
        bool interrupt,
        bool audibleInWalkieTalkie,
        bool audibleByEnemies)
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

        LogVerbose(
            $"Playing audio clip: {clipToPlay.name} for type '{audioClipType}' on AudioSource '{audioSourceType}' in {GetType().Name}.");

        if (interrupt) selectedAudioSource.Stop();
        
        selectedAudioSource.PlayOneShot(clipToPlay);
        
        if (audibleInWalkieTalkie)
            WalkieTalkie.TransmitOneShotAudio(selectedAudioSource, clipToPlay, selectedAudioSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(selectedAudioSource.transform.position);
    }

    /// <summary>
    /// Checks and outs any players that are nearby.
    /// </summary>
    /// <param name="radius">Unity units of the sphere radius.</param>
    /// <param name="players">The list of nearby players.</param>
    /// <returns>A bool on if any players are nearby.</returns>
    protected bool GetPlayersCloseBy(float radius, out List<PlayerControllerB> players)
    {
        players = [];

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if ((transform.position - player.transform.position).magnitude <= radius)
            {
                players.Add(player);
            }
        }

        return players.Count != 0;
    }

    // https://discussions.unity.com/t/how-can-i-tell-when-a-navmeshagent-has-reached-its-destination/52403/5
    protected bool HasFinishedAgentPath()
    {
        return !agent.pathPending || !(agent.remainingDistance > agent.stoppingDistance) ||
               (!agent.hasPath && agent.velocity.sqrMagnitude == 0f);
    }

    protected static Vector3 GetRandomPositionOnNavMesh(Vector3 position, float radius = 10f)
    {
        return RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(position, radius, layerMask: -1,
            randomSeed: new System.Random());
    }

    protected Vector3 GetRandomPositionNearPlayer(PlayerControllerB player, float radius = 15f, float minDistance = 0f)
    {
        return GetRandomPositionOnNavMesh(player.transform.position + Random.insideUnitSphere * radius +
                                          Random.onUnitSphere * minDistance);
    }

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
    /// Converts a <see cref="PathStatus"/> value to a boolean.
    /// </summary>
    /// <param name="status">The path status to convert.</param>
    /// <returns>
    /// <c>true</c> if the path status is <see cref="PathStatus.Valid"/> or <see cref="PathStatus.ValidButInLos"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    internal static bool PathStatusToBool(PathStatus status)
    {
        return status is PathStatus.Valid or PathStatus.ValidButInLos;
    }

    /// <summary>
    /// Checks if the AI can construct a valid path to the given position.
    /// </summary>
    /// <param name="agent">The NavMeshAgent to construct the path for.</param>
    /// <param name="position">The target position to path to.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path is obstructed by line of sight.</param>
    /// <param name="bufferDistance">The buffer distance within which the path is considered valid without further checks.</param>
    /// <returns>Returns true if the agent can path to the position within the buffer distance or if a valid path exists; otherwise, false.</returns>
    internal static PathStatus IsPathValid(
        NavMeshAgent agent,
        Vector3 position,
        bool checkLineOfSight = false,
        float bufferDistance = 0f)
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
            if (Vector3.Distance(path.corners[^1],
                    RoundManager.Instance.GetNavMeshPosition(position, RoundManager.Instance.navHit, 2.7f)) > 1.5)
                return PathStatus.ValidButInLos;

            for (int i = 1; i < path.corners.Length; ++i)
            {
                if (Physics.Linecast(path.corners[i - 1], path.corners[i], 262144))
                {
                    return PathStatus.ValidButInLos;
                }
            }

            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (PlayerUtil.IsPlayerDead(player)) continue;
                if (player.HasLineOfSightToPosition(position, 70f, 80, 1)) return PathStatus.ValidButInLos;
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
    /// <returns>The transform of the closest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    internal static Transform GetClosestValidNodeToPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f)
    {
        return GetValidNodeFromPosition(
            findClosest: true,
            pathStatus: out pathStatus,
            agent: agent,
            position: position,
            allAINodes: allAINodes,
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
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
    /// <param name="ignoredAINodes">A collection of AI node game objects to ignore.</param>
    /// <param name="checkLineOfSight">Whether to check if any segment of the path to the node is obstructed by line of sight.</param>
    /// <param name="allowFallbackIfBlocked">If true, allows finding another node if the first is blocked by line of sight.</param>
    /// <param name="bufferDistance">The minimum distance a node must be from the position to be considered.</param>
    /// <returns>The transform of the farthest valid AI node that the agent can path to, or null if no valid node is found.</returns>
    internal static Transform GetFarthestValidNodeFromPosition(
        out PathStatus pathStatus,
        NavMeshAgent agent,
        Vector3 position,
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes = null,
        bool checkLineOfSight = false,
        bool allowFallbackIfBlocked = false,
        float bufferDistance = 1f)
    {
        return GetValidNodeFromPosition(
            findClosest: false,
            pathStatus: out pathStatus,
            agent: agent,
            position: position,
            allAINodes: allAINodes,
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
    /// <param name="allAINodes">A collection of all AI node game objects to consider.</param>
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
        IEnumerable<GameObject> allAINodes,
        IEnumerable<GameObject> ignoredAINodes,
        bool checkLineOfSight,
        bool allowFallbackIfBlocked,
        float bufferDistance)
    {
        HashSet<GameObject> ignoredNodesSet = ignoredAINodes == null ? [] : [..ignoredAINodes];

        List<GameObject> aiNodes = allAINodes
            .Where(node =>
                !ignoredNodesSet.Contains(node) && Vector3.Distance(position, node.transform.position) > bufferDistance)
            .ToList();

        aiNodes.Sort((a, b) =>
        {
            float distanceA = Vector3.Distance(position, a.transform.position);
            float distanceB = Vector3.Distance(position, b.transform.position);
            return findClosest ? distanceA.CompareTo(distanceB) : distanceB.CompareTo(distanceA);
        });

        foreach (GameObject node in aiNodes)
        {
            pathStatus = IsPathValid(agent, node.transform.position, checkLineOfSight);
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
                    PathStatus fallbackStatus = IsPathValid(agent, fallbackNode.transform.position);
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

    internal void LogVerbose(object message)
    {
        if (BiodiversityPlugin.Config.VerboseLogging)
            BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");
    }

    internal void LogError(object message)
    {
        BiodiversityPlugin.Logger.LogError($"{GetLogPrefix()} {message}");
    }

    internal void LogWarning(object message)
    {
        BiodiversityPlugin.Logger.LogWarning($"{GetLogPrefix()} {message}");
    }

    protected virtual string GetLogPrefix()
    {
        return $"[{enemyType.enemyName}]";
    }
}