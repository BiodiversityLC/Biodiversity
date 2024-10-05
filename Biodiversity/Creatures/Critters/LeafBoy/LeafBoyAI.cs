using Biodiversity.Util.Types;
using System;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = UnityEngine.Random;
using RoleInGroup = Biodiversity.Creatures.Critters.LeafBoy.LeafBoySharedData.RoleInGroup;

namespace Biodiversity.Creatures.Critters.LeafBoy;

public class LeafBoyAI : BiodiverseAI
{
    private static readonly int AnimationIdHash = Animator.StringToHash("AnimationId");

#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioClip[] happySfx;
    [SerializeField] private AudioClip[] scaredSfx;
    [SerializeField] private AudioClip[] hitSfx;
    [SerializeField] private AudioClip[] burySfx;
#pragma warning restore 0649

    private enum AudioClipTypes
    {
        Happy,
        Scared,
        Hit,
        Bury
    }
    
    private enum AudioSourceTypes
    {
        CreatureVoice,
        CreatureSfx,
    }

    private enum States
    {
        BeingLeader,
        FollowingLeader,
        RunningAway,
        Buried,
        Dead,
    }
    
    private static CritterConfig Config => CritterHandler.Instance.Config;

    private RoleInGroup _roleInGroup;

    internal readonly NullableObject<LeafBoyGroup> Group = new();

    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _scaryPlayerDistance;
    private float _playerForgetTime;
    private float _timeSinceSeenPlayer;
    private float _timeUntilNextLaughSfx = 5;

    protected override void Awake()
    {
        base.Awake();
        // todo: change this to a function and then make AudioClips and AudioSources private in the parent class
        AudioClips[AudioClipTypes.Happy.ToString()] = happySfx;
        AudioClips[AudioClipTypes.Scared.ToString()] = scaredSfx;
        AudioClips[AudioClipTypes.Hit.ToString()] = hitSfx;
        AudioClips[AudioClipTypes.Bury.ToString()] = burySfx;
        
        AudioSources[AudioSourceTypes.CreatureVoice.ToString()] = creatureVoice;
        AudioSources[AudioSourceTypes.CreatureSfx.ToString()] = creatureSFX;
    }

    public override void Start()
    {
        base.Start();
        Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

        _scaryPlayerDistance = Config.LeafBoyScaryPlayerDistance;
        _playerForgetTime = Config.LeafBoyPlayerForgetTime;

        _roleInGroup = LeafBoySharedData.Instance.AssignLeafBoy(this);
        Group.Value = LeafBoySharedData.Instance.GetGroup(this);

        InitializeState(_roleInGroup == RoleInGroup.Leader ? States.BeingLeader : States.FollowingLeader);
    }

    public override void Update()
    {
        if (isEnemyDead) return;
        base.Update();
        if (!IsServer) return;

        _timeSinceSeenPlayer += Time.deltaTime;
        _timeUntilNextLaughSfx -= Time.deltaTime;

        if (_timeUntilNextLaughSfx < 0)
        {
            _timeUntilNextLaughSfx = Random.Range(20f, 90f);
            PlayAudioClipTypeServerRpc(AudioClipTypes.Happy.ToString(), AudioSourceTypes.CreatureVoice.ToString(), audibleByEnemies: true);
        }
        
        MoveWithAcceleration();
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        if (!IsServer || isEnemyDead) return;
        
        switch (currentBehaviourStateIndex)
        {
            case (int)States.BeingLeader:
            {
                break;
            }
            
            case (int)States.FollowingLeader:
            {
                break;
            }
        }
        
        ScanForPlayers();
    }

    private void LateUpdate()
    {
        CheckAnimations();
    }

    public bool IsNarrowPath()
    {
        float halfMinimumSpaceNeeded = Group.Value.CurrentFormation.Value.MinimumHorizontalSpaceNeeded / 2;
        return Physics.Raycast(transform.position, transform.right, halfMinimumSpaceNeeded)
               || Physics.Raycast(transform.position, -transform.right, halfMinimumSpaceNeeded);
    }

    private void UpdateLeaderPathHistory()
    {
        
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
        // if (GetPlayersCloseBy(_scaryPlayerDistance, out List<PlayerControllerB> _))
        // {
        //     _timeSinceSeenPlayer = 0;
        //
        //     if (currentBehaviourStateIndex == (int)States.Roaming) PlayAudioClipTypeServerRpc(AudioClipTypes.Scared.ToString(), AudioSourceTypes.CreatureVoice.ToString(), audibleByEnemies: true);
        //     SwitchBehaviourState(States.Running);
        // }
        // else if (currentBehaviourStateIndex == (int)States.Running) SwitchBehaviourState(States.Scared);
    }

    private void InitializeState(States state)
    {
        switch (state)
        {
            case States.BeingLeader or States.FollowingLeader:
            {
                _agentMaxSpeed = Config.LeafBoyNormalSpeed;
                _agentMaxAcceleration = Config.LeafBoyNormalAcceleration;
                
                break;
            }

            case States.RunningAway:
            {
                _agentMaxSpeed = Config.LeafBoyScaredSpeed;
                _agentMaxAcceleration = Config.LeafBoyScaredAcceleration;
                
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