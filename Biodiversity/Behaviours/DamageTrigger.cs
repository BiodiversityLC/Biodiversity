using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Behaviours;

public class DamageTrigger : MonoBehaviour {
	[SerializeField]
	int enemyAttackForce = 1;

	[SerializeField]
	int playerDamage = 20;

	[SerializeField]
	float damageDelay = 1;
    
	[NonSerialized]
	public List<EnemyAI> enemiesToIgnore = [];

	float damageTime = 0;

	List<EnemyAI> enemiesToHit = [];
	bool hitLocalPlayer = false;
	
	void Update() {
		if(enemiesToHit.Count == 0 && !hitLocalPlayer) return;
		damageTime += Time.deltaTime;

		if (damageTime >= damageDelay) {
			damageTime = 0;
			if (hitLocalPlayer) {
				GameNetworkManager.Instance.localPlayerController.DamagePlayer(playerDamage);
			}

			if(!GameNetworkManager.Instance.localPlayerController.IsHost) return;
			
			foreach (EnemyAI enemy in enemiesToHit) {
				enemy.HitEnemyOnLocalClient(enemyAttackForce);
			}
		}
	}

	void OnTriggerEnter(Collider other) {
		if (other.TryGetComponent(out EnemyAI enemy) && !enemiesToIgnore.Contains(enemy)) {
			if (!enemiesToHit.Contains(enemy))
				enemiesToHit.Add(enemy);
		}

		if (other.TryGetComponent(out PlayerControllerB player) && GameNetworkManager.Instance.localPlayerController == player) {
			hitLocalPlayer = true;
		}
	}
	
	void OnTriggerExit(Collider other) {
		if (other.TryGetComponent(out EnemyAI enemy) && !enemiesToIgnore.Contains(enemy)) {
			if (enemiesToHit.Contains(enemy))
				enemiesToHit.Remove(enemy);
		}

		if (other.TryGetComponent(out PlayerControllerB player) && GameNetworkManager.Instance.localPlayerController == player) {
			hitLocalPlayer = false;
		}
	}
}