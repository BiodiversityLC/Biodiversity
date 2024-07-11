using System.Linq;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class AvoidingPlayerState : BehaviourState
{
    private float _avoidPlayerAudioTimer;
    private float _avoidPlayerIntervalTimer;

    private bool _shouldTransitionToAttacking;

    private float _avoidPlayerTimerTotal;

    public AvoidingPlayerState(AloeServer aloeServerInstance) : base(aloeServerInstance)
    {
        Transitions =
        [
            new TransitionToPreviousState(aloeServerInstance, this),
            new TransitionToAttackingState(aloeServerInstance, this)
        ];
    }

    public override void OnStateEnter()
    {
        AloeServerInstance.agentMaxSpeed = 9f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.agent.acceleration = 50f;
        AloeServerInstance.openDoorSpeedMultiplier = 20f;

        _avoidPlayerAudioTimer = 4.1f;
        _avoidPlayerIntervalTimer = 5;
        _avoidPlayerTimerTotal = 0f;

        if (AloeServerInstance.overridePlaySpottedAnimation) AloeServerInstance.overridePlaySpottedAnimation = false;
        else
            AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(AloeServerInstance.aloeId,
                AloeClient.Spotted);

        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.HasFinishedSpottedAnimation, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeServerInstance.netcodeController.PlayAudioClipTypeServerRpc(
            AloeServerInstance.aloeId, AloeClient.AudioClipTypes.InterruptedHealing);
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            AloeServerInstance.aloeId,
            0.1f,
            0.5f);
    }

    public override void UpdateBehaviour()
    {
        // Make the Aloe stay still until the spotted animation is finished
        if (!AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value)
        {
            if (AloeServerInstance.AvoidingPlayer.IsNotNull)
                AloeServerInstance.LookAtPosition(AloeServerInstance.AvoidingPlayer.Value.transform.position);

            AloeServerInstance.moveTowardsDestination = false;
            return;
        }

        AloeServerInstance.moveTowardsDestination = true;
        _avoidPlayerAudioTimer -= Time.deltaTime;
        _avoidPlayerTimerTotal += Time.deltaTime;
    }

    public override void AIIntervalBehaviour()
    {
        if (!AloeServerInstance.netcodeController.HasFinishedSpottedAnimation.Value)
            return;
        
        PlayerControllerB tempPlayer = AloeUtils.GetClosestPlayerLookingAtPosition(
            AloeServerInstance.eye.transform, logSource: AloeServerInstance.Mls);
        if (tempPlayer != null)
        {
            AloeServerInstance.AvoidingPlayer.Value = tempPlayer;
            if (_avoidPlayerAudioTimer <= 0)
            {
                _avoidPlayerAudioTimer = 4.1f;
                AloeServerInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerInstance.aloeId,
                    AloeClient.AudioClipTypes.InterruptedHealing);
            }

            _avoidPlayerTimerTotal = 0f;
        }

        float waitTimer = 5f;
        _avoidPlayerIntervalTimer -= Time.deltaTime;
        if (_avoidPlayerIntervalTimer > 0) return;

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
            _shouldTransitionToAttacking = false;
        }
        else
        {
            _shouldTransitionToAttacking = true;
        }

        _avoidPlayerIntervalTimer = waitTimer;
    }

    public override void OnStateExit()
    {
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

            Vector3 closestPlayerPosition = AloeUtils.GetClosestPlayerFromList(
                    players: StartOfRound.Instance.allPlayerScripts.ToList(),
                    transform: AloeServerInstance.transform,
                    inputPlayer: null, 
                logSource: AloeServerInstance.Mls).transform.position;

            return Vector3.Distance(AloeServerInstance.transform.position, closestPlayerPosition) <= 12f;
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
           return avoidingPlayerState._shouldTransitionToAttacking;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.AttackingPlayer;
        }
    }
}