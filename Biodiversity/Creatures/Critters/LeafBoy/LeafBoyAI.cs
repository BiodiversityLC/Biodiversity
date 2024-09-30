using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = UnityEngine.Random;

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

    [Header("AI and Pathfinding")] [Space(5f)] 
    [SerializeField] private AISearchRoutine wanderRoutine;
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
        Roaming,
        Running,
        Scared
    }
    
    protected override Dictionary<string, AudioClip[]> AudioClips { get; } = new();
    
    private static CritterConfig Config => CritterHandler.Instance.Config;

    private float _agentMaxSpeed;
    private float _agentMaxAcceleration;
    private float _scaryPlayerDistance;
    private float _playerForgetTime;
    private float _timeSinceSeenPlayer;
    private float _timeUntilNextLaughSfx = 5;

    private void Awake()
    {
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

        if (!IsServer) return;
        if (isEnemyDead || !agent.isOnNavMesh) return;

        if (!wanderRoutine.inProgress)
            StartSearch(transform.position, wanderRoutine);

        ScanForPlayers();

        switch (currentBehaviourStateIndex)
        {
            case (int)States.Roaming:
                break;

            case (int)States.Running:
                break;

            case (int)States.Scared:
                if (_timeSinceSeenPlayer > _playerForgetTime)
                    SwitchBehaviourState(States.Roaming);

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
        if (GetPlayersCloseBy(_scaryPlayerDistance, out List<PlayerControllerB> _))
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
                _agentMaxSpeed = Config.LeafBoyNormalSpeed;
                _agentMaxAcceleration = Config.LeafBoyNormalAcceleration;
                
                break;
            }

            case States.Scared:
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
}