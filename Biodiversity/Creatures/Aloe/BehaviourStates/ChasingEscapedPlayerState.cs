using Biodiversity.Creatures.Aloe.Types;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class ChasingEscapedPlayerState : BehaviourState
{
    public float WaitBeforeChasingTimer;

    private bool _isPlayerTargetable;
    
    public ChasingEscapedPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToPassiveRoaming(aloeServerInstance, this),
            new TransitionToKidnappingPlayer(aloeServerInstance, this)
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 6f;
        AloeServerInstance.agentMaxAcceleration = 12f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 2f;
        AloeServerInstance.inGrabAnimation = false;
        
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0.9f, 0.5f);
        AloeServerInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerInstance.aloeId, AloeClient.AudioClipTypes.Chase, true);
        AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(AloeServerInstance.aloeId, AloeClient.Stand);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);

        WaitBeforeChasingTimer = AloeServerInstance.WaitBeforeChasingEscapedPlayerTime;
        _isPlayerTargetable = true;
    }

    public override void UpdateBehaviour()
    {
        WaitBeforeChasingTimer -= Time.deltaTime;
    }

    public override void AIIntervalBehaviour()
    {
        _isPlayerTargetable = true;
        if (WaitBeforeChasingTimer <= 0)
        {
            if (!AloeServerInstance.movingTowardsTargetPlayer)
            {
                AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0f, 0.25f);
            }
            
            if (AloeUtils.IsPlayerTargetable(AloeServerInstance.ActualTargetPlayer.Value))
            {
                AloeServerInstance.movingTowardsTargetPlayer = true;
            }
            else
            {
                _isPlayerTargetable = false;
            }
        }
        
        if (AloeUtils.DoesEyeHaveLineOfSightToPosition(
                     pos: AloeServerInstance.ActualTargetPlayer.Value.transform.position, 
                     eye: AloeServerInstance.eye, 
                     width: AloeServerInstance.ViewWidth, 
                     range: AloeServerInstance.ViewRange, 
                     logSource: AloeServerInstance.Mls))
        {
            AloeServerInstance.netcodeController.LookTargetPosition.Value =
                AloeServerInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
            if (WaitBeforeChasingTimer <= 0)
                AloeServerInstance.LookAtPosition(AloeServerInstance.ActualTargetPlayer.Value.transform.position);
        }
        else
        {
            AloeServerInstance.netcodeController.LookTargetPosition.Value = AloeServerInstance.GetLookAheadVector();
        }
    }

    private class TransitionToKidnappingPlayer(AloeServer aloeServerInstance, ChasingEscapedPlayerState chasingEscapedPlayerState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            if (chasingEscapedPlayerState.WaitBeforeChasingTimer > 0 ||
                Vector3.Distance(AloeServerInstance.ActualTargetPlayer.Value.transform.position,
                    AloeServerInstance.transform.position) > 1.5f) return false;
            
            AloeServerInstance.LogDebug("Player is close to aloe! Kidnapping him now.");
            AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(
                AloeServerInstance.aloeId,
                AloeClient.Grab);
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
            return AloeServer.States.Roaming;
        }
    }
}