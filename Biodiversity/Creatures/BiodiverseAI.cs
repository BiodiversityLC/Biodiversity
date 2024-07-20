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
        Collider[] hitColliders = new Collider[StartOfRound.Instance.allPlayerScripts.Length]; // don't hardcode to 4 as to not break lobby expansion mods
        if(Physics.OverlapSphereNonAlloc(base.transform.position, radius, hitColliders, 8, QueryTriggerInteraction.Ignore) <= 0) { // no clue what the 8 means but it was in the thumpers code lol
            players = [];
            return false;
        }
        players = hitColliders.Select(collider => collider.GetComponent<PlayerControllerB>()).ToList();
        return true;
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
