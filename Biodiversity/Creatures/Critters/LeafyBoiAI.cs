using System.Collections.Generic;
using Biodiversity.General;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace Biodiversity.Creatures.Critters;

public class LeafyBoiAI : BiodiverseAI {
    // TODO: Make these config
	private const int SCARY_PLAYER_DISTANCE = 6;
    private const float BASE_MOVEMENT_SPEED = 1.5F;
    private const float SCARED_SPEED_MULTIPLIER = 4F;
    const float PLAYER_FORGET_TIME = 3f;

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
            if(_state == value) return;
            LogVerbose($"Updating state: {_state} -> {value}");
            _state = value;
        }
    }
    
    public override void DoAIInterval() {
        base.DoAIInterval();

        CheckAnimations();

        if (!IsHost) return;

        if (!agent.isOnNavMesh) return;

        if(!wanderRoutine.inProgress)
            StartSearch(transform.position, wanderRoutine);
        
        ScanForPlayers();
        
        switch (State) {
            case AIState.WANDERING:
                agent.speed = BASE_MOVEMENT_SPEED;
                break;
            
            case AIState.RUNNING:
                // run.
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
                break;
            
            case AIState.SCARED:
                agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
                
                if (timeSinceSeenPlayer > PLAYER_FORGET_TIME) {
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
        if (GetPlayersCloseBy(SCARY_PLAYER_DISTANCE, out List<PlayerControllerB> players)) { // player nearby
            timeSinceSeenPlayer = 0;
            State = AIState.RUNNING;
        } else if (State == AIState.RUNNING) {
            State = AIState.SCARED;
        }
    }
}