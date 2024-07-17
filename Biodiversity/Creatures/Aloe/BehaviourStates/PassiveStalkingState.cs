using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class PassiveStalkingState : BehaviourState
{
    private bool _isStaringAtTargetPlayer;
    private bool _isPlayerReachable;
    
    public PassiveStalkingState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(aloeServerInstance, this),
            new TransitionToPassiveRoaming(aloeServerInstance, this),
            new TransitionToStalkingPlayerToKidnap(aloeServerInstance),
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 5f;
        AloeServerInstance.agentMaxAcceleration = 70f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.moveTowardsDestination = true;
        AloeServerInstance.openDoorSpeedMultiplier = 4f;

        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, true);

        _isPlayerReachable = true;
        _isStaringAtTargetPlayer = false;
        
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            AloeServerInstance.aloeId, 0f);
    }

    public override void AIIntervalBehaviour()
    {
        _isPlayerReachable = true;
        if (!AloeServerInstance.ActualTargetPlayer.IsNotNull) return;
        
        // See if the aloe can stare at the player
        if (Vector3.Distance(AloeServerInstance.transform.position, AloeServerInstance.ActualTargetPlayer.Value.transform.position) <= AloeServerInstance.PassiveStalkStaredownDistance &&
            !Physics.Linecast(AloeServerInstance.eye.position, AloeServerInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position,
                StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            AloeServerInstance.LogDebug("Aloe is staring at player");
            if (!_isStaringAtTargetPlayer) AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
                AloeServerInstance.aloeId, 0.8f);
            
            AloeServerInstance.moveTowardsDestination = false;
            AloeServerInstance.movingTowardsTargetPlayer = false;
            _isStaringAtTargetPlayer = true;
        }
        // If she cant stare, then go and find the player
        else
        {
            if (_isStaringAtTargetPlayer) AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
                AloeServerInstance.aloeId, 0, 0.1f);
            _isStaringAtTargetPlayer = false;
            
            if (AloeUtils.IsPlayerReachable(
                    agent: AloeServerInstance.agent, 
                    player: AloeServerInstance.ActualTargetPlayer.Value, 
                    transform: AloeServerInstance.transform, 
                    eye: AloeServerInstance.eye, 
                    viewWidth: AloeServerInstance.ViewWidth, 
                    viewRange: AloeServerInstance.ViewRange, 
                    logSource: AloeServerInstance.Mls))
            {
                Transform closestNodeToPlayer = AloeUtils.GetClosestValidNodeToPosition(
                    pathStatus: out AloeUtils.PathStatus pathStatus,
                    agent: AloeServerInstance.agent,
                    position: AloeServerInstance.ActualTargetPlayer.Value.transform.position,
                    allAINodes: AloeServerInstance.allAINodes,
                    ignoredAINodes: null,
                    checkLineOfSight: true,
                    allowFallbackIfBlocked: false,
                    bufferDistance: 0f,
                    AloeServerInstance.Mls);

                if (pathStatus == AloeUtils.PathStatus.Invalid) AloeServerInstance.moveTowardsDestination = false;
                else AloeServerInstance.SetDestinationToPosition(closestNodeToPlayer.position);
            }
            else
            {
                _isPlayerReachable = false;
            }
        }
        
        AloeServerInstance.LogDebug($"Is player reachable: {_isPlayerReachable}");
    }
    
    private class TransitionToAvoidingPlayer(AloeServer aloeServerInstance, PassiveStalkingState passiveStalkingState)
        : StateTransition(aloeServerInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;
        
        public override bool ShouldTransitionBeTaken()
        {
            // Check if a player sees the aloe
            _playerLookingAtAloe = AloeUtils.GetClosestPlayerLookingAtPosition
                (AloeServerInstance.eye.transform, logSource: AloeServerInstance.Mls);
            return _playerLookingAtAloe != null;
        }
        
        public override AloeServer.States NextState()
        {
            return AloeServer.States.AvoidingPlayer;
        }
        
        public override void OnTransition()
        {
            AloeServerInstance.AvoidingPlayer.Value = _playerLookingAtAloe;
            AloeServerInstance.timesFoundSneaking++;
            
            // Greatly increase fear level if the player turns around to see the Aloe starting at them
            if (passiveStalkingState._isStaringAtTargetPlayer &&
                AloeServerInstance.ActualTargetPlayer.Value == AloeServerInstance.AvoidingPlayer.Value)
                AloeServerInstance.netcodeController.IncreasePlayerFearLevelClientRpc(
                    AloeServerInstance.aloeId, 0.8f, AloeServerInstance.AvoidingPlayer.Value.playerClientId);
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServer aloeServerInstance,
        PassiveStalkingState passiveStalkingState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            if (AloeUtils.IsPlayerDead(AloeServerInstance.ActualTargetPlayer.Value))
            {
                AloeServerInstance.LogDebug("Player that I was stalking is dead, switching back to passive roaming.");
                return true;
            }

            if (!passiveStalkingState._isPlayerReachable)
            {
                AloeServerInstance.LogDebug("Player that I was stalking isn't reachable, switching back to passive roaming.");
                return true;
            }
            
            return false;

        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.Roaming;
        }
    }
    
    private class TransitionToStalkingPlayerToKidnap(AloeServer aloeServerInstance)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return AloeServerInstance.ActualTargetPlayer.Value.health <=
                   AloeServerInstance.PlayerHealthThresholdForHealing;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.AggressiveStalking;
        }
    }
}