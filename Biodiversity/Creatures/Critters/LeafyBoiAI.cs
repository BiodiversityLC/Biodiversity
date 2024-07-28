using Biodiversity.General;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Biodiversity.Creatures.Critters;

public class LeafyBoiAI : BiodiverseAI {
	private const int SCARY_PLAYER_DISTANCE = 6;
    private const float BASE_MOVEMENT_SPEED = 1.5F;
    private const float SCARED_SPEED_MULTIPLIER = 4F;

    [SerializeField]
    private float forgetScaryPlayersTimer;

    private float timeSinceSeenPlayer;
    private static readonly int _AnimationIdHash = Animator.StringToHash("AnimationId");

    AISearchRoutine wanderRoutine = new AISearchRoutine();

    public enum AIState {
        WANDERING, // normal
        RUNNING, // actively running
        SCARED // recently ran away.
    }

    AIState _state = AIState.WANDERING;

    public AIState State {
        get => _state;
        set {
            LogVerbose($"Updating state: {_state} -> {value}");
            _state = value;
        }
    }
    
    public override void DoAIInterval() {
        base.DoAIInterval();

        CheckAnimations();

        if (!IsHost) return;

        if (!agent.isOnNavMesh) return;

        switch (State) {
            case AIState.WANDERING:
                agent.speed = BASE_MOVEMENT_SPEED;
                if(!wanderRoutine.inProgress)
                    StartSearch(transform.position, wanderRoutine);
                
                ScanForPlayers();
                break;
            
            case AIState.RUNNING:
                // run.
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
                break;
            
            case AIState.SCARED:
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
                ScanForPlayers();

                if (timeSinceSeenPlayer > forgetScaryPlayersTimer) {
                    State = AIState.WANDERING;
                }
                
                break;
        }
    }

    public override void Update() {
        base.Update();
        timeSinceSeenPlayer += Time.deltaTime;
    }

    private void CheckAnimations() {
        var velocity = agent.velocity;

        var xVelocity = velocity.x;
        var zVelocity = velocity.z;

        if (xVelocity.Approx(0, 0.1F) && zVelocity.Approx(0, 0.1F)) {
            creatureAnimator.SetInteger(_AnimationIdHash, 0);
            return;
        }

        creatureAnimator.SetInteger(_AnimationIdHash, currentBehaviourStateIndex + 1);
    }

    private void ScanForPlayers() {
        var playersInLineOfSight = GetAllPlayersInLineOfSight(range: SCARY_PLAYER_DISTANCE);

        if (playersInLineOfSight.Length != 0) { // player in sight
            if (State == AIState.RUNNING) return; // we are already running away.

            State = AIState.RUNNING;
            return;
        }

        if (State != AIState.SCARED) {
            State = AIState.SCARED;
            StopSearch(currentSearch); // restart search
            // TODO: maybe find a more appropriate point to run to?
        }

        timeSinceSeenPlayer = 0;
    }
}