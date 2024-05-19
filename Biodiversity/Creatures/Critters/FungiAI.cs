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
			BiodiversityPlugin.Logger.LogDebug("[Fungi] Starting new search.");
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
		BiodiversityPlugin.Logger.LogDebug("[Fungi] Stunning.");

		yield return new WaitForSeconds(Config.FungiStunTime);

		BiodiversityPlugin.Logger.LogDebug("[Fungi] Stopping stun.");

		isStunned = false;
		agent.isStopped = false;
		speedBoostTime = Config.FungiBoostTime;
	}
}