using System.Collections;
using Biodiversity.General;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

public class FungiAI : BiodiverseAI {
	AISearchRoutine wanderRoutine = new AISearchRoutine();

	float speedBoostTime;
	bool isStunned;

	static CritterConfig Config => CritterHandler.Instance.Config;
	
	public override void DoAIInterval() {
		base.DoAIInterval();
		if(isStunned) return;

		speedBoostTime -= Time.deltaTime;
		agent.speed = speedBoostTime > 0 ? Config.FungiBoostedSpeed : Config.FungiNormalSpeed;
        
		if(!isStunned && wanderRoutine == null || !wanderRoutine.inProgress) {
			LogVerbose("[Fungi] Starting new search.");
			StartSearch(transform.position, wanderRoutine);
		}
	}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1) {
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);

		if (IsHost) {
			StartCoroutine(ApplySpeedBoost());
		}
	}

	IEnumerator ApplySpeedBoost() {
		isStunned = true;
		moveTowardsDestination = false;
		agent.isStopped = true;
		StopSearch(wanderRoutine, true);
		LogVerbose("[Fungi] Stunning.");

		yield return new WaitForSeconds(Config.FungiStunTime);

		LogVerbose("[Fungi] Stopping stun.");

		isStunned = false;
		agent.isStopped = false;
		speedBoostTime = Config.FungiBoostTime;
	}

	internal override float GetDelayBeforeContinueSearch() {
		return Random.Range(5f, 10f);
	}
}