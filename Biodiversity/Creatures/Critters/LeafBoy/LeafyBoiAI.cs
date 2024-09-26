using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Critters.LeafBoy;

public class LeafyBoiAI : BiodiverseAI
{
    private static readonly int AnimationIdHash = Animator.StringToHash("AnimationId");

#pragma warning disable 0649
    [Header("Audio")] [Space(5f)] 
    [SerializeField] private AudioClip[] scaredSfx;
    [SerializeField] private AudioClip[] barkSfx;

    [Header("AI and Pathfinding")] [Space(5f)] 
    [SerializeField] private AISearchRoutine wanderRoutine;
#pragma warning restore 0649

    private enum AudioClipTypes
    {
        Bark,
        Scared
    }
    
    private enum AudioSourceTypes
    {
        CreatureVoice,
        CreatureSfx,
    }

    private enum States
    {
        Roaming, // normal
        Running, // actively running
        Scared // recently ran away
    }

    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _scaryPlayerDistance;
    private float _baseMovementSpeed;
    private float _scaredSpeedMultiplier;
    private float _playerForgetTime;
    
    protected override Dictionary<string, AudioClip[]> AudioClips { get; } = new();

    private static CritterConfig Config => CritterHandler.Instance.Config;

    private States _state = States.Roaming;

    private States State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            LogVerbose($"Updating state: {_state} -> {value}");
            _state = value;
        }
    }

    private float _timeSinceSeenPlayer;
    private float _timeUntilNextBarkSfx = 5;

    private void Awake()
    {
        // todo: change this to a function and then make AudioClips and AudioSources private in the parent class
        AudioClips[AudioClipTypes.Bark.ToString()] = barkSfx;
        AudioClips[AudioClipTypes.Scared.ToString()] = scaredSfx;
        
        AudioSources[AudioSourceTypes.CreatureVoice.ToString()] = creatureVoice;
        AudioSources[AudioSourceTypes.CreatureSfx.ToString()] = creatureSFX;
    }

    public override void Start()
    {
        base.Start();
        Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

        _scaryPlayerDistance = Config.LeafBoyScaryPlayerDistance;
        _baseMovementSpeed = Config.LeafBoyBaseMovementSpeed;
        _scaredSpeedMultiplier = Config.LeafBoyScaredSpeedMultiplier;
        _playerForgetTime = Config.LeafBoyPlayerForgetTime;
    }

    public override void Update()
    {
        if (isEnemyDead) return;
        base.Update();
        if (!IsServer) return;

        _timeSinceSeenPlayer += Time.deltaTime;
        _timeUntilNextBarkSfx -= Time.deltaTime;

        if (_timeUntilNextBarkSfx < 0)
        {
            _timeUntilNextBarkSfx = Random.Range(40f, 90f);
            PlayAudioClipTypeServerRpc(AudioClipTypes.Bark.ToString(), AudioSourceTypes.CreatureVoice.ToString(), audibleByEnemies: true);
        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (!IsServer) return;
        if (isEnemyDead || !agent.isOnNavMesh) return;

        if (!wanderRoutine.inProgress)
            StartSearch(transform.position, wanderRoutine);

        ScanForPlayers();

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
                agent.speed = _baseMovementSpeed;
                break;

            case (int)States.Running:
                agent.speed = _baseMovementSpeed * _scaredSpeedMultiplier;
                break;

            case (int)States.Scared:
                agent.speed = _baseMovementSpeed * _scaredSpeedMultiplier;

                if (_timeSinceSeenPlayer > _playerForgetTime)
                    State = States.Roaming;

                break;
        }
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
        if (GetPlayersCloseBy(_scaryPlayerDistance, out List<PlayerControllerB> players))
        {
            _timeSinceSeenPlayer = 0;

            if (currentBehaviourStateIndex == (int)States.Roaming) PlayAudioClipTypeServerRpc(AudioClipTypes.Scared.ToString(), AudioSourceTypes.CreatureVoice.ToString(), audibleByEnemies: true);
            SwitchBehaviourState(States.Running);
        }
        else if (currentBehaviourStateIndex == (int)States.Running) SwitchBehaviourState(States.Scared);
    }

    private void InitializeState(States state)
    {
        switch (state)
        {
            case States.Roaming:
            {
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
}