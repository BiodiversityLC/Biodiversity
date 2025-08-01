﻿using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public class SlapCollisionDetection : MonoBehaviour
{
#pragma warning disable 0649
    [SerializeField] private AudioSource slapAudioSource;
    [SerializeField] private AudioClip slapSfx;
#pragma warning restore 0649

    private readonly HashSet<ulong> _playersAlreadyHitBySlap = [];
    private readonly HashSet<int> _enemiesAlreadyHitBySlap = [];

    private bool _playedSlapSfx;
    private bool _canBeSlapped;

    private void OnTriggerStay(Collider other)
    {
        if (!_canBeSlapped) return;
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player))
        {
            if (_playersAlreadyHitBySlap.Contains(player.actualClientId)) return;
            
            SlapPlayerServerRpc(player.actualClientId);
        }
        else if (other.CompareTag("Enemy") && other.TryGetComponent(out EnemyAICollisionDetect enemyAICollisionDetect))
        {
            EnemyAI enemy = enemyAICollisionDetect.mainScript;
            if (!enemy) return;
            if (_enemiesAlreadyHitBySlap.Contains(enemy.thisEnemyIndex)) return;

            SlapEnemyServerRpc(enemy.thisEnemyIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SlapEnemyServerRpc(int enemyIndex)
    {
        EnemyAI enemyToDamage = null;
        for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
        {
            EnemyAI enemy = RoundManager.Instance.SpawnedEnemies[i];
            if (enemy.thisEnemyIndex == enemyIndex)
            {
                enemyToDamage = enemy;
                break;
            }
        }

        enemyToDamage?.HitEnemy(AloeHandler.Instance.Config.SlapDamageEnemies,
            hitID: 998); // 998 is the Aloe's bestiary ID

        SlapEnemyClientRpc(enemyIndex);
    }

    [ClientRpc]
    private void SlapEnemyClientRpc(int enemyIndex)
    {
        _enemiesAlreadyHitBySlap.Add(enemyIndex);
        
        if (_playedSlapSfx) return;
        _playedSlapSfx = true;
        slapAudioSource.PlayOneShot(slapSfx);
        WalkieTalkie.TransmitOneShotAudio(slapAudioSource, slapSfx, slapAudioSource.volume);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SlapPlayerServerRpc(ulong playerId)
    {
        PlayerControllerB playerToDamage = PlayerUtil.GetPlayerFromClientId(playerId);
        playerToDamage.DamagePlayer(AloeHandler.Instance.Config.SlapDamagePlayers, true, true, CauseOfDeath.Bludgeoning,
            force: playerToDamage.turnCompass.forward * (-1 * 5) + playerToDamage.turnCompass.right * 5);

        SlapPlayerClientRpc(playerId);
    }

    [ClientRpc]
    private void SlapPlayerClientRpc(ulong playerId)
    {
        _playersAlreadyHitBySlap.Add(playerId);

        if (_playedSlapSfx) return;
        _playedSlapSfx = true;
        slapAudioSource.PlayOneShot(slapSfx);
        WalkieTalkie.TransmitOneShotAudio(slapAudioSource, slapSfx, slapAudioSource.volume);
    }

    public void EnableSlap()
    {
        _canBeSlapped = true;
        _playedSlapSfx = false;
        _playersAlreadyHitBySlap.Clear();
        _enemiesAlreadyHitBySlap.Clear();
    }

    public void DisableSlap()
    {
        _canBeSlapped = false;
        _playedSlapSfx = false;
        _playersAlreadyHitBySlap.Clear();
        _enemiesAlreadyHitBySlap.Clear();
    }
}