using System.Collections;
using Biodiversity.Behaviours;
using Biodiversity.General;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

public class FungiAI : BiodiverseAI {
	[Header("Spore")]
	[SerializeField]
	GameObject sporeCloudPrefab;

	[SerializeField]
	Transform sporeCloudOrigin;
    
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
		LogVerbose("[Fungi] spewing all over.");

		SpewSporeClientRPC();
        
		yield return new WaitForSeconds(Config.FungiStunTime);

		LogVerbose("[Fungi] stun is over, ");
		StunOverClientRPC();
		
		isStunned = false;
		agent.isStopped = false;
		speedBoostTime = Config.FungiBoostTime;
	}

	[ClientRpc]
	void SpewSporeClientRPC() {
		creatureAnimator.SetTrigger("spew");
	}

	// triggered in animation event
	public void SpawnSpores() {
		GameObject spores = Instantiate(sporeCloudPrefab, sporeCloudOrigin.position, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
		spores.GetComponent<DamageTrigger>().enemiesToIgnore.Add(this);
		spores.GetComponent<Animation>().Play(); // this is fucked
	}
    
	[ClientRpc]
	void StunOverClientRPC() {
		creatureAnimator.SetTrigger("stun_over");
	}
	// fixme: kinda doesn't work at all lol
	internal override float GetDelayBeforeContinueSearch() {
		return 0;
	}
}