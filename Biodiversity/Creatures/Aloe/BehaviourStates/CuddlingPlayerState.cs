using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class CuddlingPlayerState : BehaviourState
{
    public CuddlingPlayerState(AloeServer aloeServerInstance) : base(aloeServerInstance)
    {
        Transitions =
        [
            
        ];
    }

    public override void OnStateEnter()
    {
        AloeServerInstance.agent.speed = 0;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 4f;
    }

    public override void AIIntervalBehaviour()
    {
        PlayerControllerB tempPlayer = AloeUtils.GetClosestPlayerLookingAtPosition(
            transform: AloeServerInstance.eye.transform, 
            ignorePlayer: AloeServerInstance.ActualTargetPlayer.Value, 
            logSource: AloeServerInstance.Mls);
        
        if (tempPlayer != null)
        {
            AloeServerInstance.LookAtPosition(tempPlayer.transform.position);
        }
        else if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
        {
            AloeServerInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
        }
    }
}