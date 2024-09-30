using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Behaviours;

public class DamageTrigger : NetworkBehaviour
{
    [SerializeField] public int enemyAttackForce = 1;
    [SerializeField] public int playerDamage = 20;
    [SerializeField] public float damageDelay = 1;

    [NonSerialized] public readonly HashSet<EnemyAI> EnemiesToIgnore = [];
    [NonSerialized] public readonly HashSet<PlayerControllerB> PlayersToIgnore = [];

    private readonly Dictionary<EnemyAI, Coroutine> _enemiesToHit = [];
    private readonly Dictionary<PlayerControllerB, Coroutine> _playersToHit = [];

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out EnemyAICollisionDetect enemy) &&
            !EnemiesToIgnore.Contains(enemy.mainScript) &&
            !_enemiesToHit.ContainsKey(enemy.mainScript))
            _enemiesToHit.Add(enemy.mainScript, StartCoroutine(ApplyDamageOverTimeToEnemie(enemy.mainScript)));

        else if (other.TryGetComponent(out PlayerControllerB player) &&
                 !PlayersToIgnore.Contains(player) &&
                 !_playersToHit.ContainsKey(player))
            _playersToHit.Add(player, StartCoroutine(ApplyDamageOverTimeToPlayer(player)));
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out EnemyAICollisionDetect enemy) &&
            !EnemiesToIgnore.Contains(enemy.mainScript) &&
            _enemiesToHit.ContainsKey(enemy.mainScript))
        {
            EnemyAI enemyMainScript = enemy.mainScript;
            if (_enemiesToHit.TryGetValue(enemyMainScript, out Coroutine coroutine))
                StopCoroutine(coroutine);

            _enemiesToHit.Remove(enemyMainScript);
        }

        else if (other.TryGetComponent(out PlayerControllerB player) &&
                 !PlayersToIgnore.Contains(player) &&
                 _playersToHit.ContainsKey(player))
        {
            if (_playersToHit.TryGetValue(player, out Coroutine coroutine))
                StopCoroutine(coroutine);

            _playersToHit.Remove(player);
        }
    }

    private IEnumerator ApplyDamageOverTimeToEnemie(EnemyAI enemy)
    {
        if (!IsServer) yield break;
        while (true)
        {
            yield return new WaitForSeconds(damageDelay);
            if (enemy == null || enemy.isEnemyDead)
            {
                _enemiesToHit.Remove(enemy);
                yield break;
            }

            enemy.HitEnemyClientRpc(enemyAttackForce, -1, false, 34322);
        }
    }

    private IEnumerator ApplyDamageOverTimeToPlayer(PlayerControllerB player)
    {
        if (!IsServer) yield break;
        while (true)
        {
            yield return new WaitForSeconds(damageDelay);
            if (player == null || player.isPlayerDead)
            {
                _playersToHit.Remove(player);
                yield break;
            }

            player.DamagePlayer(playerDamage, true, true, CauseOfDeath.Suffocation);
        }
    }
}