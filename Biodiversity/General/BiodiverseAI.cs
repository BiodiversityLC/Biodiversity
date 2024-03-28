using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
    protected PlayerControllerB GetClosestPlayer(List<PlayerControllerB> players) {
        return GetClosestPlayer(players, transform.position);
    }
    protected PlayerControllerB GetClosestPlayer(List<PlayerControllerB> players, Vector3 point) {
        players.OrderBy(player => Vector3.Distance(player.transform.position, point));
        players.ForEach(player => { BiodiversityPlugin.Logger.LogInfo($"[BiodiverseAI] {player.playerUsername} at a distance of {Vector3.Distance(player.transform.position, point)}"); });
        return players.First();
    }
}
