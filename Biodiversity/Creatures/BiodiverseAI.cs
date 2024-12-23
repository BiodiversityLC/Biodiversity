﻿using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace Biodiversity.Creatures;

public abstract class BiodiverseAI : EnemyAI
{
    protected bool ShouldProcessEnemy()
    {
        return isEnemyDead || StartOfRound.Instance.allPlayersDead;
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
        return RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(position, 10f, layerMask: -1,
            randomSeed: new Random());
    }

    protected Vector3 GetRandomPositionNearPlayer(PlayerControllerB player, float radius = 15f, float minDistance = 0f)
    {
        return GetRandomPositionOnNavMesh(player.transform.position + UnityEngine.Random.insideUnitSphere * radius +
                                          UnityEngine.Random.onUnitSphere * minDistance);
    }

    protected PlayerControllerB GetClosestPlayer(IEnumerable<PlayerControllerB> players)
    {
        return GetClosestPlayer(players, transform.position);
    }

    protected PlayerControllerB GetClosestPlayer(IEnumerable<PlayerControllerB> players, Vector3 point)
    {
        return players.OrderBy(player => Vector3.Distance(player.transform.position, point)).First();
    }

    protected void LogVerbose(object message)
    {
        if (BiodiversityPlugin.Config.VerboseLogging)
        {
            BiodiversityPlugin.Logger.LogDebug($"[{enemyType.enemyName}] {message}");
        }
    }

    internal virtual float GetDelayBeforeContinueSearch()
    {
        return 0;
    }
}