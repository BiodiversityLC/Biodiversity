using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class StalkingPlayerToKidnapState : BehaviourState
{
    private bool _isPlayerReachable;
    
    public StalkingPlayerToKidnapState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(aloeServerInstance),
            new TransitionToPassiveRoaming(aloeServerInstance, this)
        ];
    }

    public override void OnStateEnter()
    {
        base.OnStateEnter();
        AloeServerInstance.agentMaxSpeed = 5f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.inGrabAnimation = false;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 4f;
        
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, true);
    }

    public override void AIIntervalBehaviour()
    {
        if (AloeServerInstance.inGrabAnimation)
        {
            AloeServerInstance.movingTowardsTargetPlayer = 
                Vector3.Distance(AloeServerInstance.transform.position, 
                    AloeServerInstance.ActualTargetPlayer.Value.transform.position) > 2f;
        }
        else
        {
            if (Vector3.Distance(AloeServerInstance.transform.position, 
                    AloeServerInstance.ActualTargetPlayer.Value.transform.position) <= 2.5f && 
                !AloeServerInstance.inGrabAnimation)
            {
                // See if the aloe can kidnap the player
                AloeServerInstance.LogDebug("Player is close to aloe! Kidnapping him now");
                AloeServerInstance.agent.speed = 0f;
                AloeServerInstance.agent.acceleration = 0f;
                AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(AloeServerInstance.aloeId, AloeClient.Grab);
                AloeServerInstance.inGrabAnimation = true;
            }
            else if (AloeUtils.IsPlayerReachable(
                         agent: AloeServerInstance.agent, 
                         player: AloeServerInstance.ActualTargetPlayer.Value, 
                         transform: AloeServerInstance.transform, 
                         eye: AloeServerInstance.eye, 
                         viewWidth: AloeServerInstance.ViewWidth, 
                         viewRange: AloeServerInstance.ViewRange, 
                         logSource: AloeServerInstance.Mls))
            {
                if (Vector3.Distance(
                        AloeServerInstance.transform.position, 
                        AloeServerInstance.ActualTargetPlayer.Value.transform.position) <= 5)
                {
                    AloeServerInstance.movingTowardsTargetPlayer = true;
                }
                else
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
                        logSource: AloeServerInstance.Mls);

                    if (pathStatus == AloeUtils.PathStatus.Invalid) AloeServerInstance.moveTowardsDestination = false;
                    else AloeServerInstance.SetDestinationToPosition(closestNodeToPlayer.position);
                }
                
                _isPlayerReachable = true;
            }
            else
            {
                _isPlayerReachable = false;
            }
        }
    }
    
    private class TransitionToAvoidingPlayer(AloeServer aloeServerInstance)
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
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServer aloeServerInstance,
        StalkingPlayerToKidnapState stalkingPlayerToKidnapState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return AloeUtils.IsPlayerDead(AloeServerInstance.ActualTargetPlayer.Value) ||
                   !stalkingPlayerToKidnapState._isPlayerReachable;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.PassiveRoaming;
        }
    }
}