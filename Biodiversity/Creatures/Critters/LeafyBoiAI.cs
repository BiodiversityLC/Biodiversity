using Biodiversity.Util;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Biodiversity.Creatures.Critters;

public class LeafyBoiAI : BiodiverseAI
{
    private float SCARY_PLAYER_DISTANCE;
    private float BASE_MOVEMENT_SPEED;
    private float SCARED_SPEED_MULTIPLIER;
    private float PLAYER_FORGET_TIME;

    private float timeSinceSeenPlayer;
    private static readonly int AnimationIdHash = Animator.StringToHash("AnimationId");

    private float timeUntilNextBarkSFX = 5;
    private float timeUntilNextStepAudibleSound = 3;

#pragma warning disable 0649
    [Header("Audio")]
    [SerializeField] private AudioClip[] scaredSFX;

    [SerializeField] private AudioClip[] randomBarkSFX;
#pragma warning restore 0649

    private AISearchRoutine wanderRoutine = new();

    public enum AIState
    {
        WANDERING, // normal
        RUNNING, // actively running
        SCARED // recently ran away.
    }

    private static CritterConfig Config => CritterHandler.Instance.Config;

    private AIState _state = AIState.WANDERING;

    public AIState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            LogVerbose($"Updating state: {_state} -> {value}");
            _state = value;
        }
    }

    enum SOUNDID
    {
        SCARED,
        BARK
    }

    [ClientRpc]
    public void PlaySFXClientRpc(int id, int rand)
    {
        switch (id)
        {
            case (int)(SOUNDID.SCARED):
                creatureSFX.PlayOneShot(scaredSFX[rand]);
                WalkieTalkie.TransmitOneShotAudio(creatureSFX, scaredSFX[rand]);
                break;
            case (int)(SOUNDID.BARK):
                creatureSFX.PlayOneShot(randomBarkSFX[rand]);
                WalkieTalkie.TransmitOneShotAudio(creatureSFX, randomBarkSFX[rand]);
                break;
        }
    }

    public override void Start()
    {
        base.Start();
        Random.InitState(StartOfRound.Instance.randomMapSeed + thisEnemyIndex);

        SCARY_PLAYER_DISTANCE = Config.LeafBoyScaryPlayerDistance;
        BASE_MOVEMENT_SPEED = Config.LeafBoyBaseMovementSpeed;
        SCARED_SPEED_MULTIPLIER = Config.LeafBoyScaredSpeedMultiplier;
        PLAYER_FORGET_TIME = Config.LeafBoyPlayerForgetTime;
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        CheckAnimations();

        if (!IsHost) return;

        if (!agent.isOnNavMesh) return;

        if (!wanderRoutine.inProgress)
            StartSearch(transform.position, wanderRoutine);

        ScanForPlayers();

        switch (State)
        {
            case AIState.WANDERING:
                agent.speed = BASE_MOVEMENT_SPEED;
                break;

            case AIState.RUNNING:
                // run.
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
                break;

            case AIState.SCARED:
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;

                if (timeSinceSeenPlayer > PLAYER_FORGET_TIME)
                {
                    State = AIState.WANDERING;
                }

                break;
        }
    }

    public override void Update()
    {
        base.Update();

        timeUntilNextStepAudibleSound -= Time.deltaTime;
        if (timeUntilNextStepAudibleSound < 0)
        {
            timeUntilNextStepAudibleSound = Random.Range(3f, 6f);
            RoundManager.Instance.PlayAudibleNoise(transform.position);
        }

        if (!IsServer) return;

        timeSinceSeenPlayer += Time.deltaTime;

        timeUntilNextBarkSFX -= Time.deltaTime;
        if (timeUntilNextBarkSFX < 0)
        {
            timeUntilNextBarkSFX = Random.Range(40f, 90f);
            PlaySFXClientRpc((int)SOUNDID.BARK, Random.Range(0, randomBarkSFX.Length));
            RoundManager.Instance.PlayAudibleNoise(transform.position);
        }
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
        if (IsPlayerCloseByToPosition(transform.position, SCARY_PLAYER_DISTANCE))
        {
            timeSinceSeenPlayer = 0;

            if (State == AIState.WANDERING) {
                PlaySFXClientRpc((int)SOUNDID.SCARED, Random.Range(0, scaredSFX.Length));
                RoundManager.Instance.PlayAudibleNoise(transform.position);
            }

            State = AIState.RUNNING;
        }
        else if (State == AIState.RUNNING)
        {
            State = AIState.SCARED;
        }
    }
    
    /// <summary>
    /// Determines whether there is a player within the specified distance to the given position.
    /// It will return true regardless of whether there are 1 or more players.
    /// </summary>
    /// <param name="position">The reference position to measure distance from.</param>
    /// <param name="playerDetectionRange">The maximum distance from the position to search for players in.</param>
    /// <returns>Returns <c>true</c> if there is at least one player within the specified distance.</returns>
    private static bool IsPlayerCloseByToPosition(Vector3 position, float playerDetectionRange)
    {
        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        
        for (int i = 0; i < players.Length; i++)
        {
            PlayerControllerB player = players[i];
            if (!PlayerUtil.IsPlayerDead(player) &&
                Vector3.Distance(player.transform.position, position) <= playerDetectionRange) return true;
        }

        return false;
    }
}