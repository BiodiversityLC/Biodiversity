using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Critters;

public class PrototaxAI : BiodiverseAI 
{
	private static readonly int Spew = Animator.StringToHash("Spewing");
	
	private static CritterConfig Config => CritterHandler.Instance.Config;
	
#pragma warning disable 0649	
	[Header("Spore")]
	[SerializeField] private GameObject sporeCloudPrefab;
	[SerializeField] private Transform sporeCloudOrigin;
	[SerializeField] private AudioSource sporeAudioSource;
	[SerializeField] private AudioClip[] sporeAmbientSfx;

	[Header("Audio")]
	[SerializeField] private AudioClip[] footstepSfx = [];
	[SerializeField] private AudioClip[] spewSfx = [];
	[SerializeField] private AudioClip[] hitSfx = [];

	[Header("AI and Pathfinding")] 
	[SerializeField] private AISearchRoutine roamSearchRoutine;
#pragma warning restore 0649
	
	private enum States
	{
		Roaming,
		Idle,
		Spewing,
		RunningAway,
	}

	private enum AudioClipTypes
	{
		Spew,
		SporeAmbient,
		Footsteps,
		Hit
	}

	private enum AudioSourceTypes
	{
		CreatureVoice,
		CreatureSfx,
		Spore
	}

	protected override Dictionary<string, AudioClip[]> AudioClips { get; } = new();

	private Vector3 _spawnPosition;

	private Vector2 _roamingTimeRange;
	private Vector2 _idleTimeRange;

	private float _agentMaxSpeed;
	private float _agentMaxAcceleration;
	private float _speedBoostTimer;
	private float _idleTimer;
	private float _roamingTimer;
	private float _takeDamageCooldown;

	private void Awake()
	{
		AudioClips[AudioClipTypes.Spew.ToString()] = spewSfx;
		AudioClips[AudioClipTypes.Footsteps.ToString()] = footstepSfx;
		AudioClips[AudioClipTypes.SporeAmbient.ToString()] = sporeAmbientSfx;
		AudioClips[AudioClipTypes.Hit.ToString()] = hitSfx;

		AudioSources[AudioSourceTypes.CreatureVoice.ToString()] = creatureVoice;
		AudioSources[AudioSourceTypes.CreatureSfx.ToString()] = creatureSFX;
		AudioSources[AudioSourceTypes.Spore.ToString()] = sporeAudioSource;
	}

	public override void Start()
	{
		Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

		_spawnPosition = transform.position;
		_roamingTimeRange = new Vector2(Config.PrototaxWanderTimeMin,
			Mathf.Max(Config.PrototaxWanderTimeMax, Config.PrototaxWanderTimeMin + 1));
		_idleTimeRange = new Vector2(Config.PrototaxIdleTimeMin,
			Mathf.Max(Config.PrototaxIdleTimeMax, Config.PrototaxIdleTimeMin + 1));
	}

	public override void Update()
	{
		base.Update();
		if (!IsServer) return;
		
		_takeDamageCooldown -= Time.deltaTime;
		MoveWithAcceleration();
	}

	public override void DoAIInterval()
	{
		base.DoAIInterval();
		if (!IsServer) return;

		switch (currentBehaviourStateIndex)
		{
			case (int)States.Roaming:
			{
				_roamingTimer -= Time.deltaTime;
				if (_roamingTimer <= 0)
				{
					SwitchBehaviourState(States.Idle);
					break;
				}
				
				break;
			}

			case (int)States.Idle:
			{
				_idleTimer -= Time.deltaTime;
				if (_idleTimer <= 0)
				{
					SwitchBehaviourState(States.Roaming);
					break;
				}
				
				break;
			}

			case (int)States.RunningAway:
			{
				_speedBoostTimer -= Time.deltaTime;
				if (_speedBoostTimer <= 0) SwitchBehaviourState(States.Roaming);
				
				break;
			}
		}
	}

	public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
		int hitId = -1)
	{
		base.HitEnemy(force, playerWhoHit, playHitSFX, hitId);
		if (!IsServer || _takeDamageCooldown > 0) return;
		
		PlayAudioClipTypeServerRpc(AudioClipTypes.Hit.ToString(), AudioSourceTypes.CreatureSfx.ToString());
		_takeDamageCooldown = 0.03f;
	}
	
	private void InitializeState(States state)
	{
		switch (state)
		{
			case States.Roaming:
			{
				_agentMaxSpeed = Config.PrototaxNormalSpeed;
				_agentMaxAcceleration = Config.PrototaxNormalAcceleration;
				_roamingTimer = Random.Range(_roamingTimeRange.x, _roamingTimeRange.y);
				
				if (previousBehaviourStateIndex == (int)States.RunningAway && roamSearchRoutine.inProgress) break;
				StartSearch(Config.PrototaxAnchoredWandering ? _spawnPosition : transform.position, roamSearchRoutine);
				
				break;
			}

			case States.Idle:
			{
				if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
				
				agent.speed = 0f;
				agent.acceleration = 40f;
				_agentMaxSpeed = 0f;
				_agentMaxAcceleration = Config.PrototaxNormalAcceleration;
				moveTowardsDestination = false;
				_idleTimer = Random.Range(_idleTimeRange.x, _idleTimeRange.y);

				break;
			}

			case States.Spewing:
			{
				if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
				
				agent.speed = 0f;
				agent.acceleration = 40f;
				_agentMaxSpeed = 0f;
				_agentMaxAcceleration = Config.PrototaxNormalAcceleration;
				moveTowardsDestination = false;

				break;
			}

			case States.RunningAway:
			{
				agent.speed *= 1.25f;
				agent.acceleration *= 1.25f;
				_agentMaxSpeed = Config.PrototaxBoostedSpeed;
				_agentMaxAcceleration = Config.PrototaxBoostedAcceleration;
				_speedBoostTimer = Config.PrototaxBoostTime;

				if (roamSearchRoutine.inProgress) StopSearch(roamSearchRoutine);
				
				// todo: add offset for these node functions
				// todo: make a better system for the input of ai nodes
				// todo: make it automatically just use your agent
				StartSearch(GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position, roamSearchRoutine);
				break;
			}
		}
	}
	
	private void SwitchBehaviourState(States state)
	{
		int intState = (int)state;
		if (currentBehaviourStateIndex == intState) return;
		previousBehaviourStateIndex = currentBehaviourStateIndex;
		currentBehaviourStateIndex = intState;
		InitializeState(state);
	}
	
	private void MoveWithAcceleration()
	{
		float speedAdjustment = Time.deltaTime / 2f;
		agent.speed = Mathf.Lerp(agent.speed, _agentMaxSpeed, speedAdjustment);

		float accelerationAdjustment = Time.deltaTime;
		agent.acceleration = Mathf.Lerp(agent.acceleration, _agentMaxAcceleration, accelerationAdjustment);
	}
}