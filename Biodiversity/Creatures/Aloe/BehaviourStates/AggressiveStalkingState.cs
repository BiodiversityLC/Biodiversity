using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.AloeStates.AggressiveStalking)]
internal class AggressiveStalkingState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private bool _isPlayerReachable;

    private const float GrabAnimationAgentMaxSpeed = 2f;
    private const float NormalAgentMaxSpeed = 5f;

    private const float GrabAnimationAgentMaxAcceleration = 200f;
    private const float NormalAgentMaxAcceleration = 50f;

    public AggressiveStalkingState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(EnemyAIInstance),
            new TransitionToPassiveRoaming(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = NormalAgentMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = NormalAgentMaxAcceleration;
        EnemyAIInstance.InGrabAnimation = false;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 4f;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, true);

        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0f,
            0.3f);
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        _isPlayerReachable = true;

        if (EnemyAIInstance.InGrabAnimation)
        {
            EnemyAIInstance.AgentMaxSpeed = GrabAnimationAgentMaxSpeed;
            EnemyAIInstance.AgentMaxAcceleration = GrabAnimationAgentMaxAcceleration;
            EnemyAIInstance.agent.acceleration = GrabAnimationAgentMaxAcceleration;

            float distanceToGrabbingPlayer = Vector3.Distance(EnemyAIInstance.transform.position,
                EnemyAIInstance.ActualTargetPlayer.Value.transform.position);
            EnemyAIInstance.movingTowardsTargetPlayer = distanceToGrabbingPlayer > 3f;
            EnemyAIInstance.LogVerbose($"Distance to grabbing player: {distanceToGrabbingPlayer}");
        }
        else
        {
            EnemyAIInstance.AgentMaxSpeed = NormalAgentMaxSpeed;
            EnemyAIInstance.AgentMaxAcceleration = NormalAgentMaxAcceleration;

            if (Vector3.Distance(EnemyAIInstance.transform.position,
                    EnemyAIInstance.ActualTargetPlayer.Value.transform.position) <= 3f &&
                !EnemyAIInstance.InGrabAnimation)
            {
                // See if the aloe can kidnap the player
                EnemyAIInstance.LogVerbose("Player is close to aloe! Kidnapping him now");
                EnemyAIInstance.agent.speed = 0f;
                EnemyAIInstance.agent.acceleration = 0f;
                EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(EnemyAIInstance.BioId, AloeClient.Grab);
                EnemyAIInstance.InGrabAnimation = true;
            }

            else if (EnemyAIInstance.IsPlayerReachable(
                         player: EnemyAIInstance.ActualTargetPlayer.Value,
                         eyeTransform: EnemyAIInstance.eye,
                         viewWidth: EnemyAIInstance.ViewWidth,
                         viewRange: EnemyAIInstance.ViewRange))
            {
                if (Vector3.Distance(
                        EnemyAIInstance.transform.position,
                        EnemyAIInstance.ActualTargetPlayer.Value.transform.position) <= 5)
                {
                    EnemyAIInstance.movingTowardsTargetPlayer = true;
                }
                else
                {
                    Transform closestNodeToPlayer = BiodiverseAI.GetClosestValidNodeToPosition(
                        pathStatus: out BiodiverseAI.PathStatus pathStatus,
                        agent: EnemyAIInstance.agent,
                        position: EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                        allAINodes: EnemyAIInstance.allAINodes,
                        ignoredAINodes: null,
                        checkLineOfSight: true,
                        allowFallbackIfBlocked: false,
                        bufferDistance: 0f);

                    if (pathStatus == BiodiverseAI.PathStatus.Invalid)
                        EnemyAIInstance.moveTowardsDestination = false;
                    else EnemyAIInstance.SetDestinationToPosition(closestNodeToPlayer.position);
                }
            }
            else
            {
                _isPlayerReachable = false;
            }
        }
    }

    private class TransitionToAvoidingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;

        internal override bool ShouldTransitionBeTaken()
        {
            // Check if a player sees the aloe
            _playerLookingAtAloe = EnemyAIInstance.GetClosestPlayerLookingAtPosition(EnemyAIInstance.eye.transform.position);
            return _playerLookingAtAloe != null;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.AvoidingPlayer;
        }

        internal override void OnTransition()
        {
            EnemyAIInstance.AvoidingPlayer.Value = _playerLookingAtAloe;
            EnemyAIInstance.TimesFoundSneaking++;
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServerAI enemyAIInstance,
        AggressiveStalkingState aggressiveStalkingState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            bool isPlayerDead = PlayerUtil.IsPlayerDead(EnemyAIInstance.ActualTargetPlayer.Value);
            EnemyAIInstance.LogVerbose($"Is player dead?: {isPlayerDead}");
            return isPlayerDead || !aggressiveStalkingState._isPlayerReachable;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.Roaming;
        }
    }
}