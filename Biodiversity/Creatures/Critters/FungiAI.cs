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
	private GameObject sporeCloudPrefab;

	[SerializeField] 
	private Transform sporeCloudOrigin;

	[Header("Footstep Audio")]
	[SerializeField]
	private AudioSource footstepSource;

	[SerializeField]
	private AudioClip[] footstepSFX = [];
	
	private AISearchRoutine wanderRoutine = new();

	private float speedBoostTime;
	private bool isStunned;
	
	private static readonly int Spew = Animator.StringToHash("spew");
	private static readonly int StunOver = Animator.StringToHash("stun_over");

	private static CritterConfig Config => CritterHandler.Instance.Config;
    
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

	private IEnumerator ApplySpeedBoost() {
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
	private void SpewSporeClientRPC() {
		creatureAnimator.SetTrigger(Spew);
	}

	// triggered in animation event
	public void SpawnSpores() {
		GameObject spores = Instantiate(sporeCloudPrefab, sporeCloudOrigin.position, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
		spores.GetComponent<DamageTrigger>().enemiesToIgnore.Add(this);
		spores.GetComponent<Animation>().Play(); // this is fucked
		RoundManager.Instance.PlayAudibleNoise(transform.position);
	}
    
	[ClientRpc]
	private void StunOverClientRPC() {
		creatureAnimator.SetTrigger(StunOver);
	}
	
	public override void AnimationEventA() {
		base.AnimationEventA();

		AudioClip clip = footstepSFX[Random.Range(0, footstepSFX.Length)];
		footstepSource.PlayOneShot(clip);
		WalkieTalkie.TransmitOneShotAudio(footstepSource, clip, 1f);
	}
}