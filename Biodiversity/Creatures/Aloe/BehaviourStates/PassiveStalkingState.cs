using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.PassiveStalking)]
internal class PassiveStalkingState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    private bool _isPlayerReachable;

    public PassiveStalkingState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(EnemyAIInstance),
            new TransitionToPassiveRoaming(EnemyAIInstance, this),
            new TransitionToStalkingPlayerToKidnap(EnemyAIInstance),
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.StalkingMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.StalkingMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.moveTowardsDestination = true;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, true);

        _isPlayerReachable = true;
        EnemyAIInstance.IsStaringAtTargetPlayer = false;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        _isPlayerReachable = true;
        if (!EnemyAIInstance.ActualTargetPlayer.HasValue) return;

        // See if the aloe can stare at the player
        if (Vector3.Distance(
                EnemyAIInstance.transform.position,
                EnemyAIInstance.ActualTargetPlayer.Value.transform.position) <=
            EnemyAIInstance.PassiveStalkStaredownDistance &&
            !Physics.Linecast(EnemyAIInstance.eye.position,
                EnemyAIInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            EnemyAIInstance.moveTowardsDestination = false;
            EnemyAIInstance.movingTowardsTargetPlayer = false;
            EnemyAIInstance.IsStaringAtTargetPlayer = true;
        }
        
        // If she cant stare, then go and find the player
        else
        {
            EnemyAIInstance.IsStaringAtTargetPlayer = false;

            if (EnemyAIInstance.IsPlayerReachable(
                    player: EnemyAIInstance.ActualTargetPlayer.Value,
                    eyeTransform: EnemyAIInstance.eye,
                    viewWidth: EnemyAIInstance.ViewWidth,
                    viewRange: EnemyAIInstance.ViewRange))
            {
                Transform closestNodeToPlayer = BiodiverseAI.GetClosestValidNodeToPosition(
                    pathStatus: out BiodiverseAI.PathStatus pathStatus,
                    agent: EnemyAIInstance.agent,
                    position: EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    givenAiNodes: EnemyAIInstance.allAINodes,
                    ignoredAINodes: null,
                    checkLineOfSight: true,
                    allowFallbackIfBlocked: false,
                    bufferDistance: 0f);

                if (pathStatus == BiodiverseAI.PathStatus.Invalid) EnemyAIInstance.moveTowardsDestination = false;
                else EnemyAIInstance.SetDestinationToPosition(closestNodeToPlayer.position);
            }
            else
            {
                _isPlayerReachable = false;
            }
        }
    }

    private class TransitionToAvoidingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;

        internal override bool ShouldTransitionBeTaken()
        {
            // Check if a player sees the aloe
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

            // Greatly increase fear level if the player turns around to see the Aloe starting at them
            if (EnemyAIInstance.IsStaringAtTargetPlayer &&
                EnemyAIInstance.ActualTargetPlayer.Value == EnemyAIInstance.AvoidingPlayer.Value)
                EnemyAIInstance.netcodeController.IncreasePlayerFearLevelClientRpc(0.8f, EnemyAIInstance.AvoidingPlayer.Value.playerClientId);

            EnemyAIInstance.IsStaringAtTargetPlayer = false;
        }
    }

    private class TransitionToPassiveRoaming(AloeServerAI enemyAIInstance, PassiveStalkingState passiveStalkingState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (PlayerUtil.IsPlayerDead(EnemyAIInstance.ActualTargetPlayer.Value))
            {
                EnemyAIInstance.LogVerbose("Player that I was stalking is dead, switching back to passive roaming.");
                return true;
            }

            if (!passiveStalkingState._isPlayerReachable)
            {
                EnemyAIInstance.LogVerbose(
                    "Player that I was stalking isn't reachable, switching back to passive roaming.");
                return true;
            }

            return false;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.Roaming;
        }
    }

    private class TransitionToStalkingPlayerToKidnap(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return EnemyAIInstance.ActualTargetPlayer.Value.health <=
                   EnemyAIInstance.PlayerHealthThresholdForHealing;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.AggressiveStalking;
        }
    }
}