using System.Linq;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class AvoidingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private readonly NullableObject<PlayerControllerB> _playerLookingAtAloe = new();

    private float _avoidPlayerIntervalTimer;
    private float _avoidPlayerTimerTotal;

    private bool _shouldTransitionToAttacking;

    protected AvoidingPlayerState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
            new TransitionToPreviousState(EnemyAIInstance, this),
            new TransitionToAttackingState(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = 9f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
        EnemyAIInstance.agent.acceleration = 50f;
        EnemyAIInstance.openDoorSpeedMultiplier = 20f;

        _avoidPlayerIntervalTimer = 0f;
        _avoidPlayerTimerTotal = 0f;

        AloeSharedData.Instance.Unbind(EnemyAIInstance, BindType.Stalk);

        if (initData.ContainsKey("overridePlaySpottedAnimation") && initData.Get<bool>("overridePlaySpottedAnimation"))
        {
            ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation, true);
        }
        else
        {
            ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation, false);
            EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(EnemyAIInstance.BioId,
                AloeClient.Spotted);
        }

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            EnemyAIInstance.BioId,
            0.9f,
            0.5f);
    }

    internal override void UpdateBehaviour()
    {
        // Make the Aloe stay still until the spotted animation is finished
        if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value)
        {
            if (EnemyAIInstance.AvoidingPlayer.IsNotNull)
            {
                EnemyAIInstance.LookAtPosition(EnemyAIInstance.AvoidingPlayer.Value.transform.position);
                EnemyAIInstance.netcodeController.LookTargetPosition.Value =
                    EnemyAIInstance.AvoidingPlayer.Value.gameplayCamera.transform.position;
            }

            EnemyAIInstance.moveTowardsDestination = false;
            return;
        }

        // This only triggers on the first frame after the spotted animation has been completed
        if (!EnemyAIInstance.moveTowardsDestination)
        {
            ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.LookTargetPosition,
                EnemyAIInstance.GetLookAheadVector());
            EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0,
                1f);
        }

        EnemyAIInstance.moveTowardsDestination = true;
        _avoidPlayerTimerTotal += Time.deltaTime;
    }

    internal override void AIIntervalBehaviour()
    {
        if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value)
            return;

        _playerLookingAtAloe.Value = EnemyAIInstance.GetClosestPlayerLookingAtPosition(
            EnemyAIInstance.eye.transform.position);
        if (_playerLookingAtAloe.IsNotNull)
        {
            EnemyAIInstance.AvoidingPlayer.Value = _playerLookingAtAloe.Value;
        }

        float waitTimer = 5f;
        _avoidPlayerIntervalTimer -= Time.deltaTime;
        if (_avoidPlayerIntervalTimer > 0) return;
        _shouldTransitionToAttacking = false;

        BiodiverseAI.PathStatus pathStatus = BiodiverseAI.PathStatus.Unknown;
        Transform farAwayNode = EnemyAIInstance.AvoidingPlayer.IsNotNull
            ? BiodiverseAI.GetFarthestValidNodeFromPosition(
                pathStatus: out pathStatus,
                agent: EnemyAIInstance.agent,
                position: EnemyAIInstance.AvoidingPlayer.Value.transform.position,
                allAINodes: EnemyAIInstance.allAINodes,
                ignoredAINodes: null,
                checkLineOfSight: true,
                allowFallbackIfBlocked: true,
                bufferDistance: 2.5f)
            : null;

        if (farAwayNode != null && pathStatus != BiodiverseAI.PathStatus.Unknown &&
            pathStatus != BiodiverseAI.PathStatus.Invalid)
        {
            if (pathStatus == BiodiverseAI.PathStatus.ValidButInLos)
            {
                waitTimer = 2f;
                EnemyAIInstance.LogVerbose("Valid escape node found, but was in LOS.");
            }

            EnemyAIInstance.LogVerbose($"Setting escape node to {farAwayNode.position}.");
            EnemyAIInstance.SetDestinationToPosition(farAwayNode.position);
        }
        else
        {
            if (EnemyAIInstance.AvoidingPlayer.IsNotNull) _shouldTransitionToAttacking = true;
        }

        _avoidPlayerIntervalTimer = waitTimer;
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        EnemyAIInstance.AvoidingPlayer.Value = null;
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation, false);
    }

    private class TransitionToPreviousState(AloeServerAI enemyAIInstance, AvoidingPlayerState avoidingPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            float avoidTimerCompareValue = EnemyAIInstance.TimesFoundSneaking % 3 != 0 ? 11f : 21f; // todo: make this less dumb
            if (avoidingPlayerState._avoidPlayerTimerTotal > avoidTimerCompareValue) return true;
            if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value) return false;

            Vector3 closestPlayerPosition = EnemyAIInstance.GetClosestPlayerFromList(
                players: StartOfRound.Instance.allPlayerScripts.ToList(),
                position: EnemyAIInstance.transform.position,
                inputPlayer: null).transform.position;

            float distanceToClosestPlayer =
                Vector3.Distance(EnemyAIInstance.transform.position, closestPlayerPosition);
            return distanceToClosestPlayer > 35f &&
                   avoidingPlayerState._avoidPlayerTimerTotal >= 5f &&
                   !avoidingPlayerState._playerLookingAtAloe.IsNotNull;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return EnemyAIInstance.PreviousState.GetStateType();
        }
    }

    private class TransitionToAttackingState(AloeServerAI enemyAIInstance, AvoidingPlayerState avoidingPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value &&
                   avoidingPlayerState._shouldTransitionToAttacking;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.AttackingPlayer;
        }
    }
}