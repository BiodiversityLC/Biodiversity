using System;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Critters.LeafBoy;

public class LeafBoyAI : BiodiverseAI
{
    private static readonly int AnimationIdHash = Animator.StringToHash("AnimationId");

#pragma warning disable 0649
    [SerializeField] private AISearchRoutine roamRoutine;
    
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioClip[] happySfx;
    [SerializeField] private AudioClip[] scaredSfx;
    [SerializeField] private AudioClip[] hitSfx;
    [SerializeField] private AudioClip[] burySfx;
#pragma warning restore 0649

    private enum States
    {
        Roaming,
        RunningAway,
        Buried,
        Dead,
    }
    
    private static CritterConfig Config => CritterHandler.Instance.Config;

    private Vector3 _spawnPosition;
    private Vector3 _runAwayPosition;

    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _scaryPlayerDistance;
    private float _playerForgetTime;
    private float _timeSinceSeenPlayer;
    private float _timeUntilNextLaughSfx = 5;

    public override void Start()
    {
        base.Start();

        _scaryPlayerDistance = Config.LeafBoyScaryPlayerDistance;
        _playerForgetTime = Config.LeafBoyPlayerForgetTime;

        _spawnPosition = transform.position;
    }

    public override void Update()
    {
        if (isEnemyDead) return;
        base.Update();
        if (!IsServer) return;
        
        _timeUntilNextLaughSfx -= Time.deltaTime;

        if (_timeUntilNextLaughSfx < 0)
        {
            _timeUntilNextLaughSfx = Random.Range(20f, 90f);
            PlayRandomAudioClipTypeServerRpc(happySfx.ToString(), creatureVoice.ToString(), audibleByEnemies: true);
        }

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                MoveWithAcceleration();
                break;
            }
            
            case (int)States.RunningAway:
            {
                _timeSinceSeenPlayer += Time.deltaTime;
                MoveWithAcceleration();
                break;
            }
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer || isEnemyDead) return;
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
            {
                ScanForPlayers();
                break;
            }
            
            case (int)States.RunningAway:
            {
                if (_timeSinceSeenPlayer >= _playerForgetTime || Vector3.Distance(transform.position, _runAwayPosition) <= 3)
                    SwitchBehaviourState(States.Roaming);
                break;
            }
        }
        
        ScanForPlayers();
    }

    private void LateUpdate()
    {
        CheckAnimations();
    }
    
    private void CheckAnimations()
    {
        Vector3 velocity = agent.velocity;

        float xVelocity = velocity.x;
        float zVelocity = velocity.z;

        if (xVelocity.Approx(0, 0.1F) && zVelocity.Approx(0, 0.1F))
        {
            creatureAnimator.SetInteger(AnimationIdHash, 0);
            return;
        }

        creatureAnimator.SetInteger(AnimationIdHash, currentBehaviourStateIndex + 1);
    }
    
    private void ScanForPlayers()
    {
        if (IsPlayerCloseByToPosition(transform.position, _scaryPlayerDistance))
        {
            if (currentBehaviourStateIndex == (int)States.Roaming) PlayRandomAudioClipTypeServerRpc(scaredSfx.ToString(), creatureVoice.ToString(), audibleByEnemies: true);
            SwitchBehaviourState(States.RunningAway);
        }
    }

    private void InitializeState(States state)
    {
        switch (state)
        {
            case States.Roaming:
            {
                if (roamRoutine.inProgress) StopSearch(roamRoutine);
                
                _agentMaxSpeed = Config.LeafBoyNormalSpeed;
                _agentMaxAcceleration = Config.LeafBoyNormalAcceleration;
                
                StartSearch(Config.LeafBoyAnchoredWandering ? _spawnPosition : transform.position, roamRoutine);
                
                break;
            }

            case States.RunningAway:
            {
                if (roamRoutine.inProgress) StopSearch(roamRoutine);
                
                agent.speed *= 1.25f;
                agent.acceleration *= 1.25f;
                _agentMaxSpeed = Config.LeafBoyScaredSpeed;
                _agentMaxAcceleration = Config.LeafBoyScaredAcceleration;
                _timeSinceSeenPlayer = 0;

                _runAwayPosition =
                    GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position;

                if (PathStatusToBool(PathStatus.Invalid)) SwitchBehaviourState(States.Roaming);
                else SetDestinationToPosition(_runAwayPosition);
                
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

    public override bool Equals(object obj)
    {
        if (obj is LeafBoyAI other)
            return BioId.Equals(other.BioId, StringComparison.Ordinal);

        return false;
    }

    public override int GetHashCode()
    {
        return BioId.GetHashCode();
    }
}