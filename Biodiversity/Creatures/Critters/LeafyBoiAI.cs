using System.Collections.Generic;
using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Critters;

public class LeafyBoiAI : BiodiverseAI
{
    private static readonly int AnimationIdHash = Animator.StringToHash("AnimationId");

    private float _scaryPlayerDistance;
    private float _baseMovementSpeed;
    private float _scaredSpeedMultiplier;
    private float _playerForgetTime;

#pragma warning disable 0649
    [Header("Audio")] [Space(5f)] 
    [SerializeField] private AudioClip[] scaredSfx;
    [SerializeField] private AudioClip[] barkSfx;

    [Header("AI and Pathfinding")] [Space(5f)] 
    [SerializeField] private AISearchRoutine wanderRoutine;
#pragma warning restore 0649

    private enum AudioClipType
    {
        Bark,
        Scared
    }

    private enum States
    {
        Wandering, // normal
        Running, // actively running
        Scared // recently ran away
    }

    private static CritterConfig Config => CritterHandler.Instance.Config;

    private States _state = States.Wandering;

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
    private float _timeUntilNextStepAudibleSound = 3;

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
            PlayRandomSfxFromListServerRpc(AudioClipType.Bark);
        }

        // _timeUntilNextStepAudibleSound -= Time.deltaTime;
        // if (_timeUntilNextStepAudibleSound < 0) 
        // {
        //     _timeUntilNextStepAudibleSound = Random.Range(3f, 6f);
        //     RoundManager.Instance.PlayAudibleNoise(transform.position);
        // }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (!IsServer) return;
        if (isEnemyDead || !agent.isOnNavMesh) return;

        if (!wanderRoutine.inProgress)
            StartSearch(transform.position, wanderRoutine);

        ScanForPlayers();

        switch (State)
        {
            case States.Wandering:
                agent.speed = _baseMovementSpeed;
                break;

            case States.Running:
                agent.speed = _baseMovementSpeed * _scaredSpeedMultiplier;
                break;

            case States.Scared:
                agent.speed = _baseMovementSpeed * _scaredSpeedMultiplier;

                if (_timeSinceSeenPlayer > _playerForgetTime)
                    State = States.Wandering;

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

            if (State == States.Wandering) PlayRandomSfxFromListServerRpc(AudioClipType.Scared);
            State = States.Running;
        }
        else if (State == States.Running)
        {
            State = States.Scared;
        }
    }

    [ServerRpc]
    private void PlayRandomSfxFromListServerRpc(AudioClipType audioClipType)
    {
        int randomClipIndex = audioClipType switch
        {
            AudioClipType.Bark => Random.Range(0, barkSfx.Length),
            AudioClipType.Scared => Random.Range(0, scaredSfx.Length),
            _ => -1
        };

        if (randomClipIndex == -1)
        {
            BiodiversityPlugin.Logger.LogError($"Invalid audio clip with type: {audioClipType}");
            return;
        }

        PlaySfxClientRpc(audioClipType, randomClipIndex);
    }

    [ClientRpc]
    private void PlaySfxClientRpc(AudioClipType audioClipType, int sfxListIndex)
    {
        AudioClip audioClipToPlay = audioClipType switch
        {
            AudioClipType.Bark => barkSfx[sfxListIndex],
            AudioClipType.Scared => scaredSfx[sfxListIndex],
            _ => null
        };

        if (audioClipToPlay == null)
        {
            BiodiversityPlugin.Logger.LogError($"Invalid audio clip with type: {audioClipType}");
            return;
        }

        LogVerbose($"Playing audio clip: {audioClipToPlay.name}");
        creatureVoice.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(creatureVoice, audioClipToPlay);
        RoundManager.Instance.PlayAudibleNoise(transform.position);
    }
}