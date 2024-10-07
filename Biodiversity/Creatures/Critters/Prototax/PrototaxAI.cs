using Biodiversity.Behaviours;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax;

internal class PrototaxAI : BiodiverseAI
{
	private static readonly int Spewing = Animator.StringToHash("Spewing");

	private static CritterConfig Config => CritterHandler.Instance.Config;

#pragma warning disable 0649
	[Header("Spore")] [Space(5f)] 
	[SerializeField] private GameObject sporeCloudObject;
	[SerializeField] private Transform sporeCloudOrigin;

	[Header("Audio")] [Space(5f)] 
	[SerializeField] private AudioSource sporeAudioSource;
	[SerializeField] private AudioClip[] footstepSfx = [];
	[SerializeField] private AudioClip[] spewSfx = [];
	[SerializeField] private AudioClip[] hitSfx = [];
	[SerializeField] private AudioClip[] sporeAmbientSfx = [];

	[Header("AI and Pathfinding")] [Space(5f)] 
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
	
	private readonly NetworkVariable<bool> _spewingAnimationParam = new();
	private readonly NetworkVariable<bool> _sporeVisible = new();

	private CachedValue<DamageTrigger> _sporeCloudDamageTrigger;
	private CachedValue<Animation> _sporeCloudAnimation;

	private Vector3 _spawnPosition;

	private Vector2 _roamingTimeRange;
	private Vector2 _idleTimeRange;

	private float _agentMaxSpeed;
	private float _agentMaxAcceleration;
	private float _speedBoostTimer;
	private float _idleTimer;
	private float _roamingTimer;
	private float _takeDamageCooldown;
	private float _spewTimer;

	private bool _spewAnimComplete;

	protected override void Awake()
	{
		base.Awake();
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
		base.Start();
		Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

		_spawnPosition = transform.position;
		_roamingTimeRange = new Vector2(Config.PrototaxWanderTimeMin,
			Mathf.Max(Config.PrototaxWanderTimeMax, Config.PrototaxWanderTimeMin + 1));
		_idleTimeRange = new Vector2(Config.PrototaxIdleTimeMin,
			Mathf.Max(Config.PrototaxIdleTimeMax, Config.PrototaxIdleTimeMin + 1));

		_sporeCloudDamageTrigger = new CachedValue<DamageTrigger>(sporeCloudObject.GetComponent<DamageTrigger>);
		_sporeCloudAnimation = new CachedValue<Animation>(sporeCloudObject.GetComponent<Animation>);
		
		sporeCloudObject.SetActive(false);
		
		InitializeState(States.Roaming);
	}

	public override void Update()
	{
		base.Update();

		creatureAnimator.SetBool(Spewing, _spewingAnimationParam.Value);
		sporeCloudObject.SetActive(_sporeVisible.Value);

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
				if (_roamingTimer <= 0) SwitchBehaviourState(States.Idle);

				break;
			}

			case (int)States.Idle:
			{
				_idleTimer -= Time.deltaTime;
				if (_idleTimer <= 0) SwitchBehaviourState(States.Roaming);

				break;
			}

			case (int)States.Spewing:
			{
				_spewTimer -= Time.deltaTime;
				if (_spewTimer < 0 && _spewAnimComplete) SwitchBehaviourState(States.RunningAway);
				
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
		_takeDamageCooldown = 0.03f;

		if (currentBehaviourStateIndex != (int)States.Spewing)
		{
			PlayAudioClipTypeServerRpc(AudioClipTypes.Hit.ToString(), AudioSourceTypes.CreatureSfx.ToString());
			SwitchBehaviourState(States.Spewing);
		}
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
				agent.acceleration = Config.PrototaxNormalAcceleration + 20f;
				_agentMaxSpeed = 0f;
				_agentMaxAcceleration = Config.PrototaxNormalAcceleration + 10;
				moveTowardsDestination = false;
				_spewAnimComplete = false;
				_spewTimer = Config.PrototaxSpewTime;

				if (_sporeCloudDamageTrigger.Value == null)
				{
					LogError("Spore cloud damage trigger is null, cannot spew the spore.");
					break;
				}

				if (_sporeCloudAnimation.Value == null)
				{
					LogError("Spore cloud animation is null, cannot spew the spore.");
					break;
				}

				_sporeCloudDamageTrigger.Value.EnemiesToIgnore.Clear();
				_sporeCloudDamageTrigger.Value.EnemiesToIgnore.Add(this);

				sporeCloudObject.transform.SetParent(sporeCloudOrigin, false);

				_spewingAnimationParam.Value = true;

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
				StartSearch(
					GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position,
					roamSearchRoutine);
				break;
			}
		}
	}

	internal void SpewAnimationComplete()
	{
		if (!IsServer) return;
		sporeCloudObject.transform.SetParent(null, true);
		_spewAnimComplete = true;
	}

	public void OnAnimationEventSpewSpore()
	{
		if (!IsServer) return;
		_sporeVisible.Value = true;
		PlaySporeAnimClientRpc();
		
		PlayAudioClipTypeServerRpc(AudioClipTypes.SporeAmbient.ToString(), AudioSourceTypes.Spore.ToString());
		PlayAudioClipTypeServerRpc(AudioClipTypes.Spew.ToString(), AudioSourceTypes.CreatureVoice.ToString(),
			audibleByEnemies: true);
	}

	public void OnAnimationEventDisableSpores()
	{
		if (!IsServer) return;
		_sporeVisible.Value = false;
	}

	[ClientRpc]
	private void PlaySporeAnimClientRpc()
	{
		_sporeCloudAnimation.Value.Play();
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