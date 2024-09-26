using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Behaviours;

public class DamageTrigger : MonoBehaviour
{
    [SerializeField] public int enemyAttackForce = 1;
    [SerializeField] public int playerDamage = 20;
    [SerializeField] public float damageDelay = 1;
    [NonSerialized] public readonly List<EnemyAI> EnemiesToIgnore = [];

    private readonly List<EnemyAI> _enemiesToHit = [];

    private bool _hitLocalPlayer;

    private float _damageTime;

    private void Update()
    {
        if (_enemiesToHit.Count == 0 && !_hitLocalPlayer) return;
        _damageTime += Time.deltaTime;

        if (_damageTime >= damageDelay)
        {
            _damageTime = 0;
            if (_hitLocalPlayer) GameNetworkManager.Instance.localPlayerController.DamagePlayer(playerDamage);
            if (!GameNetworkManager.Instance.localPlayerController.IsHost) return;

            foreach (EnemyAI enemy in _enemiesToHit)
            {
                enemy.HitEnemyOnLocalClient(enemyAttackForce);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out EnemyAICollisionDetect enemy) && !EnemiesToIgnore.Contains(enemy.mainScript))
        {
            if (!_enemiesToHit.Contains(enemy.mainScript))
                _enemiesToHit.Add(enemy.mainScript);
        }

        if (other.TryGetComponent(out PlayerControllerB player) &&
            GameNetworkManager.Instance.localPlayerController == player)
        {
            _hitLocalPlayer = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out EnemyAICollisionDetect enemy) && !EnemiesToIgnore.Contains(enemy.mainScript))
        {
            if (_enemiesToHit.Contains(enemy.mainScript))
                _enemiesToHit.Remove(enemy.mainScript);
        }

        if (other.TryGetComponent(out PlayerControllerB player) &&
            GameNetworkManager.Instance.localPlayerController == player)
        {
            _hitLocalPlayer = false;
        }
    }
}