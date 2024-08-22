using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public class SlapCollisionDetection : MonoBehaviour
{
    [SerializeField] private AudioSource slapAudioSource;
    [SerializeField] private AudioClip slapSfx;
    
    private readonly HashSet<ulong> _playersAlreadyHitBySlap = [];
    private readonly HashSet<int> _enemiesAlreadyHitBySlap = [];

    private bool _playedSlapSfx;
    private bool _canBeSlapped;
    
    private void OnTriggerStay(Collider other)
    {
        if (!_canBeSlapped) return;
        if (other.CompareTag("Player"))
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player == null) return;
            if (_playersAlreadyHitBySlap.Contains(player.actualClientId)) return;
            
            SlapPlayerServerRpc(player.actualClientId);
        }
        else if (other.CompareTag("Enemy"))
        {
            EnemyAICollisionDetect enemyAICollisionDetect = other.GetComponent<EnemyAICollisionDetect>();
            if (enemyAICollisionDetect == null) return;
            EnemyAI enemy = enemyAICollisionDetect.mainScript;
            if (enemy == null) return;
            if (_enemiesAlreadyHitBySlap.Contains(enemy.thisEnemyIndex)) return;
            
            SlapEnemyServerRpc(enemy.thisEnemyIndex);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SlapEnemyServerRpc(int enemyIndex)
    {
        EnemyAI enemyToDamage =
            RoundManager.Instance.SpawnedEnemies.FirstOrDefault(enemy => enemy.thisEnemyIndex == enemyIndex);
        enemyToDamage?.HitEnemy(AloeHandler.Instance.Config.SlapDamageEnemies, hitID: 998); // 998 is the Aloe's bestiary ID
        
        SlapEnemyClientRpc(enemyIndex);
    }

    [ClientRpc]
    private void SlapEnemyClientRpc(int enemyIndex)
    {
        _enemiesAlreadyHitBySlap.Add(enemyIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SlapPlayerServerRpc(ulong playerId)
    {
        PlayerControllerB playerToDamage = StartOfRound.Instance.allPlayerScripts[playerId];
        playerToDamage.DamagePlayer(AloeHandler.Instance.Config.SlapDamagePlayers, true, true, CauseOfDeath.Bludgeoning, force: playerToDamage.turnCompass.forward * (-1 * 5) + playerToDamage.turnCompass.right * 5);
        
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