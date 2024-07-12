using Biodiversity.Creatures.Aloe.Types;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class ChasingEscapedPlayerState : BehaviourState
{
    private float _waitBeforeChasingTimer;

    private bool _isPlayerTargetable;
    
    public ChasingEscapedPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToPassiveRoaming(aloeServerInstance, this),
            new TransitionToKidnappingPlayer(aloeServerInstance)
        ];
    }

    public override void OnStateEnter()
    {
        base.OnStateEnter();
        AloeServerInstance.agentMaxSpeed = 6f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 2f;
        AloeServerInstance.inGrabAnimation = false;
        
        AloeServerInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerInstance.aloeId, AloeClient.AudioClipTypes.Chase);
        
        AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(AloeServerInstance.aloeId, AloeClient.Stand);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);

        _waitBeforeChasingTimer = AloeServerInstance.WaitBeforeChasingEscapedPlayerTime;
        _isPlayerTargetable = true;
    }

    public override void UpdateBehaviour()
    {
        _waitBeforeChasingTimer -= Time.deltaTime;
    }

    public override void AIIntervalBehaviour()
    {
        if (_waitBeforeChasingTimer <= 0)
        {
            if (AloeUtils.IsPlayerTargetable(AloeServerInstance.ActualTargetPlayer.Value))
            {
                AloeServerInstance.movingTowardsTargetPlayer = true;
                _isPlayerTargetable = true;
            }

            else _isPlayerTargetable = false;
        }
        else if (AloeUtils.DoesEyeHaveLineOfSightToPosition(
                     pos: AloeServerInstance.ActualTargetPlayer.Value.transform.position, 
                     eye: AloeServerInstance.eye, 
                     width: AloeServerInstance.ViewWidth, 
                     range: AloeServerInstance.ViewRange, 
                     logSource: AloeServerInstance.Mls))
        {
            AloeServerInstance.LookAtPosition(AloeServerInstance.ActualTargetPlayer.Value.transform.position);
        }
    }

    private class TransitionToKidnappingPlayer(AloeServer aloeServerInstance)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            if (!(Vector3.Distance(AloeServerInstance.ActualTargetPlayer.Value.transform.position,
                    AloeServerInstance.transform.position) <= 1.5f)) return false;
            
            AloeServerInstance.LogDebug("Player is close to aloe! Kidnapping him now");
            // Todo: add grab animation
            return true;

        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.KidnappingPlayer;
        }
    }

    private class TransitionToPassiveRoaming(AloeServer aloeServerInstance, ChasingEscapedPlayerState chasingEscapedPlayerState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return !chasingEscapedPlayerState._isPlayerTargetable;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.PassiveRoaming;
        }
    }
}