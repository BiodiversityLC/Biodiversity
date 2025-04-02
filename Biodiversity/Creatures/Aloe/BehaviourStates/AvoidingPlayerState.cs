using System.Linq;
using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.AloeStates.AvoidingPlayer)]
internal class AvoidingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private CachedNullable<PlayerControllerB> _playerLookingAtAloe;

    private float _avoidPlayerIntervalTimer;
    private float _avoidPlayerTimerTotal;

    private bool _shouldTransitionToAttacking;

    public AvoidingPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
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

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.AvoidingPlayerMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.AvoidingPlayerMaxAcceleration;
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
    }

    internal override void UpdateBehaviour()
    {
        // Make the Aloe stay still until the spotted animation is finished
        if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value)
        {
            if (EnemyAIInstance.AvoidingPlayer.HasValue)
            {
                EnemyAIInstance.LookAtPosition(EnemyAIInstance.AvoidingPlayer.Value.transform.position);
            }

            EnemyAIInstance.moveTowardsDestination = false;
            return;
        }

        EnemyAIInstance.moveTowardsDestination = true;
        _avoidPlayerTimerTotal += Time.deltaTime;
    }

    internal override void AIIntervalBehaviour()
    {
        if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value)
            return;

        _playerLookingAtAloe.Set(BiodiverseAI.GetClosestPlayerLookingAtPosition(EnemyAIInstance.eye.transform.position));
        if (_playerLookingAtAloe.HasValue)
        {
            EnemyAIInstance.AvoidingPlayer.Set(_playerLookingAtAloe.Value);
        }

        float waitTimer = 5f;
        _avoidPlayerIntervalTimer -= Time.deltaTime;
        if (_avoidPlayerIntervalTimer > 0) return;
        _shouldTransitionToAttacking = false;

        BiodiverseAI.PathStatus pathStatus = BiodiverseAI.PathStatus.Unknown;
        Transform farAwayNode = EnemyAIInstance.AvoidingPlayer.HasValue
            ? BiodiverseAI.GetFarthestValidNodeFromPosition(
                pathStatus: out pathStatus,
                agent: EnemyAIInstance.agent,
                position: EnemyAIInstance.AvoidingPlayer.Value.transform.position,
                givenAiNodes: EnemyAIInstance.allAINodes,
                ignoredAINodes: null,
                checkLineOfSight: true,
                allowFallbackIfBlocked: true,
                bufferDistance: 5f)
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
            if (EnemyAIInstance.AvoidingPlayer.HasValue) _shouldTransitionToAttacking = true;
        }

        _avoidPlayerIntervalTimer = waitTimer;
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        EnemyAIInstance.AvoidingPlayer.Reset();
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation, false);
    }

    private class TransitionToPreviousState(AloeServerAI enemyAIInstance, AvoidingPlayerState avoidingPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (avoidingPlayerState._avoidPlayerTimerTotal < 5f) return false;
            if (!EnemyAIInstance.netcodeController.HasFinishedSpottedAnimation.Value) return false;

            Vector3 closestPlayerPosition = EnemyAIInstance.GetClosestPlayerFromListConsideringTargetPlayer(
                players: StartOfRound.Instance.allPlayerScripts.ToList(),
                position: EnemyAIInstance.transform.position,
                currentTargetPlayer: null,
                bufferDistance: 0.01f).transform.position;

            float distanceToClosestPlayer =
                Vector3.Distance(EnemyAIInstance.transform.position, closestPlayerPosition);
            
            EnemyAIInstance.LogDebug($"Distance to closest player: {distanceToClosestPlayer}");
            
            return distanceToClosestPlayer > 35f &&
                   avoidingPlayerState._avoidPlayerTimerTotal >= 5f &&
                   !avoidingPlayerState._playerLookingAtAloe.HasValue;
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