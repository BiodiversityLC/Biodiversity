using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.AggressiveStalking)]
internal class AggressiveStalkingState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    private bool _isPlayerReachable;
    private bool _inGrabAnimation;

    private const float GrabAnimationAgentMaxSpeed = 2f;
    private const float GrabAnimationAgentMaxAcceleration = 200f;

    public AggressiveStalkingState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(EnemyAIInstance, this),
            new TransitionToPassiveRoaming(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        _inGrabAnimation = false;

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.StalkingMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.StalkingMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        EnemyAIInstance.movingTowardsTargetPlayer = false;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, true);
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        _isPlayerReachable = true;

        if (_inGrabAnimation)
        {
            EnemyAIInstance.AgentMaxSpeed = GrabAnimationAgentMaxSpeed;
            EnemyAIInstance.AgentMaxAcceleration = GrabAnimationAgentMaxAcceleration;
            EnemyAIInstance.agent.acceleration = GrabAnimationAgentMaxAcceleration;

            float distanceToGrabbingPlayer = Vector3.Distance(EnemyAIInstance.transform.position,
                EnemyAIInstance.ActualTargetPlayer.Value.transform.position);
            EnemyAIInstance.movingTowardsTargetPlayer = distanceToGrabbingPlayer > 3f;
        }
        else
        {
            EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.StalkingMaxSpeed;
            EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.StalkingMaxAcceleration;

            if (Vector3.Distance(EnemyAIInstance.transform.position,
                    EnemyAIInstance.ActualTargetPlayer.Value.transform.position) <= 3f &&
                !_inGrabAnimation)
            {
                // See if the aloe can kidnap the player
                EnemyAIInstance.LogVerbose("Player is close to aloe! Kidnapping him now");
                EnemyAIInstance.agent.speed = 0f;
                EnemyAIInstance.agent.acceleration = 0f;
                EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(AloeClient.Grab);
                _inGrabAnimation = true;
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
                        givenAiNodes: EnemyAIInstance.allAINodes,
                        ignoredAINodes: null,
                        checkLineOfSight: true,
                        allowFallbackIfBlocked: false,
                        bufferDistance: 0.2f);

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

    private class TransitionToAvoidingPlayer(AloeServerAI enemyAIInstance,
        AggressiveStalkingState aggressiveStalkingState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;

        internal override bool ShouldTransitionBeTaken()
        {
            // If the ForceGrabOnceInAnimation config is enabled and the Aloe is currently in her kidnap/grab animation, then we just don't transition to the avoiding player state.
            if (aggressiveStalkingState._inGrabAnimation && AloeHandler.Instance.Config.ForceGrabOnceInAnimation)
                return false;
            
            // Check if a player sees the Aloe
            _playerLookingAtAloe = BiodiverseAI.GetClosestPlayerLookingAtPosition(EnemyAIInstance.eye.transform.position);
            return _playerLookingAtAloe != null;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.AvoidingPlayer;
        }

        internal override void OnTransition()
        {
            EnemyAIInstance.AvoidingPlayer.Set(_playerLookingAtAloe);
            EnemyAIInstance.TimesFoundSneaking++;
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServerAI enemyAIInstance,
        AggressiveStalkingState aggressiveStalkingState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            bool isPlayerDead = PlayerUtil.IsPlayerDead(EnemyAIInstance.ActualTargetPlayer.Value);
            return isPlayerDead || !aggressiveStalkingState._isPlayerReachable;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.Roaming;
        }
    }
}