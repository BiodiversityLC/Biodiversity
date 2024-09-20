using UnityEngine;

namespace Biodiversity.Creatures.Critters;

public class PrototaxAI : BiodiverseAI 
{
	private static readonly int Spew = Animator.StringToHash("Spew");
	
	private static CritterConfig Config => CritterHandler.Instance.Config;
	
#pragma warning disable 0649	
	[Header("Spore")]
	[SerializeField] private GameObject sporeCloudPrefab;
	[SerializeField] private Transform sporeCloudOrigin;

	[Header("Audio")]
	[SerializeField] private AudioClip[] footstepSfx = [];

	[Header("AI and Pathfinding")] 
	[SerializeField] private AISearchRoutine roamSearchRoutine;
#pragma warning restore 0649

	private Vector3 _spawnPosition;

	private Vector2 _roamingTimeRange;
	private Vector2 _idleTimeRange;

	private float _agentMaxSpeed;
	private float _agentMaxAcceleration;
	private float _speedBoostTimer;
	private float _idleTimer;
	private float _roamingTimer;
	private float _takeDamageCooldown;

	private enum States
	{
		Roaming,
		Idle,
		Spewing,
		RunningAway,
	}

	public override void Start()
	{
		Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);
		//todo: assign correct ai nodes

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
				_idleTimer = Config.PrototaxSpewTime;

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