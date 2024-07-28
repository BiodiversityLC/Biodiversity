using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.General;
public abstract class BiodiverseAI : EnemyAI {
    public bool ShouldProcessEnemy() {
        return isEnemyDead || StartOfRound.Instance.allPlayersDead;
    }

    /// <summary>
    /// Checks and outs any players that are nearby.
    /// </summary>
    /// <param name="radius">unity units of the sphere radius</param>
    /// <returns>
    /// A bool on if any players are nearby.
    /// </returns>
    protected bool GetPlayersCloseBy(float radius, out List<PlayerControllerB> players) {
        players = [];

        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts) {
            if ((transform.position - player.transform.position).magnitude <= radius) {
                players.Add(player);
            }
        }
        
        return players.Count != 0;
    }

    // https://discussions.unity.com/t/how-can-i-tell-when-a-navmeshagent-has-reached-its-destination/52403/5
    protected bool HasFinishedAgentPath() {
        if(!agent.pathPending) {
            if(agent.remainingDistance <= agent.stoppingDistance) {
                if(!agent.hasPath || agent.velocity.sqrMagnitude == 0f) {
                    return true;
                }
            }
        }
        return false;
    }

    protected Vector3 GetRandomPositionOnNavMesh(Vector3 position, float radius = 10f) {
        return RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(position, 10f, layerMask: -1, randomSeed: new System.Random());
    }

    protected Vector3 GetRandomPositionNearPlayer(PlayerControllerB player, float radius = 15f, float minDistance = 0f) {
        return GetRandomPositionOnNavMesh(player.transform.position + (UnityEngine.Random.insideUnitSphere * radius) + (UnityEngine.Random.onUnitSphere * minDistance));
    }

    protected PlayerControllerB GetClosestPlayer(List<PlayerControllerB> players) {
        return GetClosestPlayer(players, transform.position);
    }
    protected PlayerControllerB GetClosestPlayer(List<PlayerControllerB> players, Vector3 point) {
        return players.OrderBy(player => Vector3.Distance(player.transform.position, point)).First();
    }

    protected void LogVerbose(object message) {
        if (BiodiversityPlugin.Config.VerboseLogging) {
            BiodiversityPlugin.Logger.LogDebug($"[{enemyType.enemyName}] {message}");
        }
    }

    internal virtual float GetDelayBeforeContinueSearch() {
        return 0;
    }
}
