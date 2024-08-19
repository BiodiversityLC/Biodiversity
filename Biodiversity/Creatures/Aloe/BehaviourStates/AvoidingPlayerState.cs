using System.Linq;
using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Creatures.Aloe.Types.Networking;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class AvoidingPlayerState : BehaviourState
{
    private readonly NullableObject<PlayerControllerB> _playerLookingAtAloe = new();
    
    private float _avoidPlayerIntervalTimer;
    private float _avoidPlayerTimerTotal;

    private bool _shouldTransitionToAttacking;

    public AvoidingPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToPreviousState(aloeServerInstance, this),
            new TransitionToAttackingState(aloeServerInstance, this)
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 9f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.agent.acceleration = 50f;
        AloeServerInstance.openDoorSpeedMultiplier = 20f;
        
        _avoidPlayerIntervalTimer = 0f;
        _avoidPlayerTimerTotal = 0f;
        
        AloeSharedData.Instance.Unbind(AloeServerInstance, BindType.Stalk);
        
        if (initData.ContainsKey("overridePlaySpottedAnimation") && initData.Get<bool>("overridePlaySpottedAnimation"))
        {
            AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.HasFinishedSpottedAnimation, true);
        }
        else
        {
            AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.HasFinishedSpottedAnimation, false);
            AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(AloeServerInstance.aloeId,
                AloeClient.Spotted);
        }
        
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            AloeServerInstance.aloeId,
            0.9f,
            0.5f);
    }

    public override void UpdateBehaviour()
    {
        // Make the Aloe stay still until the spotted animation is finished
        if (!AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value)
        {
            if (AloeServerInstance.AvoidingPlayer.IsNotNull)
            {
                AloeServerInstance.LookAtPosition(AloeServerInstance.AvoidingPlayer.Value.transform.position);
                AloeServerInstance.netcodeController.LookTargetPosition.Value =
                    AloeServerInstance.AvoidingPlayer.Value.gameplayCamera.transform.position;
            }
            
            AloeServerInstance.moveTowardsDestination = false;
            return;
        }

        // This only triggers on the first frame after the spotted animation has been completed
        if (!AloeServerInstance.moveTowardsDestination)
        {
            AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.LookTargetPosition, AloeServerInstance.GetLookAheadVector());
            AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0, 1f);
        }
        
        AloeServerInstance.moveTowardsDestination = true;
        _avoidPlayerTimerTotal += Time.deltaTime;
    }

    public override void AIIntervalBehaviour()
    {
        if (!AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value)
            return;
        
        _playerLookingAtAloe.Value = AloeUtils.GetClosestPlayerLookingAtPosition(
            AloeServerInstance.eye.transform, logSource: AloeServerInstance.Mls);
        if (_playerLookingAtAloe.IsNotNull)
        {
            AloeServerInstance.AvoidingPlayer.Value = _playerLookingAtAloe.Value;
        }

        float waitTimer = 5f;
        _avoidPlayerIntervalTimer -= Time.deltaTime;
        if (_avoidPlayerIntervalTimer > 0) return;
        _shouldTransitionToAttacking = false;

        AloeUtils.PathStatus pathStatus = AloeUtils.PathStatus.Unknown;
        Transform farAwayNode = AloeServerInstance.AvoidingPlayer.IsNotNull
            ? AloeUtils.GetFarthestValidNodeFromPosition(
                pathStatus: out pathStatus,
                agent: AloeServerInstance.agent,
                position: AloeServerInstance.AvoidingPlayer.Value.transform.position,
                allAINodes: AloeServerInstance.allAINodes,
                ignoredAINodes: null,
                checkLineOfSight: true,
                allowFallbackIfBlocked: true,
                bufferDistance: 2.5f,
                logSource: AloeServerInstance.Mls)
            : null;
        
        if (farAwayNode != null && pathStatus != AloeUtils.PathStatus.Unknown &&
            pathStatus != AloeUtils.PathStatus.Invalid)
        {
            if (pathStatus == AloeUtils.PathStatus.ValidButInLos)
            {
                waitTimer = 2f;
                AloeServerInstance.LogDebug("Valid escape node found, but was in LOS.");
            }

            AloeServerInstance.LogDebug($"Setting escape node to {farAwayNode.position}.");
            AloeServerInstance.SetDestinationToPosition(farAwayNode.position);
        }
        else
        {
            if (AloeServerInstance.AvoidingPlayer.IsNotNull) _shouldTransitionToAttacking = true;
        }

        _avoidPlayerIntervalTimer = waitTimer;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        AloeServerInstance.AvoidingPlayer.Value = null;
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.HasFinishedSpottedAnimation, false);
    }

    private class TransitionToPreviousState(AloeServer aloeServerInstance, AvoidingPlayerState avoidingPlayerState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            float avoidTimerCompareValue = AloeServerInstance.timesFoundSneaking % 3 != 0 ? 11f : 21f;
            if (avoidingPlayerState._avoidPlayerTimerTotal > avoidTimerCompareValue) return true;
            if (!AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value) return false;

            Vector3 closestPlayerPosition = AloeUtils.GetClosestPlayerFromList(
                    players: StartOfRound.Instance.allPlayerScripts.ToList(),
                    transform: AloeServerInstance.transform,
                    inputPlayer: null,
                logSource: AloeServerInstance.Mls).transform.position;
            
            float distanceToClosestPlayer = Vector3.Distance(AloeServerInstance.transform.position, closestPlayerPosition);
            return distanceToClosestPlayer > 35f && 
                   avoidingPlayerState._avoidPlayerTimerTotal >= 5f &&
                   !avoidingPlayerState._playerLookingAtAloe.IsNotNull;
        }

        public override AloeServer.States NextState()
        {
            return AloeServerInstance.PreviousState.GetStateType();
        }
    }

    private class TransitionToAttackingState(AloeServer aloeServerInstance, AvoidingPlayerState avoidingPlayerState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value &&
                   avoidingPlayerState._shouldTransitionToAttacking;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.AttackingPlayer;
        }
    }
}