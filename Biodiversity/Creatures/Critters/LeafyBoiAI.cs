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

    public override void DoAIInterval() {
        base.DoAIInterval();

        CheckAnimations();

        if (!IsHost) return;

        if (!agent.isOnNavMesh) return;

        if (currentSearch is not {
                inProgress: true,
            } && currentBehaviourStateIndex is 0 or 1) StartSearch(serverPosition);

        ScanForPlayers();

        if (currentBehaviourStateIndex is 0) agent.speed = BASE_MOVEMENT_SPEED;

        if (currentBehaviourStateIndex is not 1) return;

        agent.speed = BASE_MOVEMENT_SPEED * SCARED_SPEED_MULTIPLIER;
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

        if (playersInLineOfSight is not {
                Length: > 0,
            }) {
            if (currentBehaviourStateIndex is 0) return;

            if(forgetScaryPlayersTimer > timeSinceSeenPlayer) return;
            SwitchToBehaviourStateOnLocalClient(0);
            SwitchToBehaviourClientRpc(0);
            return;
        }

        if (currentBehaviourStateIndex is not 1) {
            SwitchToBehaviourStateOnLocalClient(1);
            SwitchToBehaviourClientRpc(1);
            StopSearch(currentSearch);
        }

        timeSinceSeenPlayer = 0;
    }
}