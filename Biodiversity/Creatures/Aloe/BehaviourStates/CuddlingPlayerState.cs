using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class CuddlingPlayerState : BehaviourState
{
    public CuddlingPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agent.speed = 0;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 4f;
        
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 1, 0.5f);
    }

    public override void AIIntervalBehaviour()
    {
        PlayerControllerB tempPlayer = AloeUtils.GetClosestPlayerLookingAtPosition(
            transform: AloeServerInstance.eye.transform, 
            ignorePlayer: AloeServerInstance.ActualTargetPlayer.Value, 
            logSource: AloeServerInstance.Mls);
        
        if (tempPlayer != null)
        {
            AloeServerInstance.netcodeController.LookTargetPosition.Value =
                tempPlayer.gameplayCamera.transform.position;
            AloeServerInstance.LookAtPosition(tempPlayer.transform.position);
        }
        else if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
        {
            AloeServerInstance.netcodeController.LookTargetPosition.Value =
                AloeServerInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
            AloeServerInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
        }
        
        AloeServerInstance.netcodeController.LookTargetPosition.Value =
            AloeServerInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
    }
}