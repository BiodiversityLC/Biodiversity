using Biodiversity.Creatures.Aloe.Types;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class DeadState : BehaviourState
{
    public DeadState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            
        ];
    }

    public override void OnStateEnter()
    {
        base.OnStateEnter();
        
        AloeServerInstance.SetTargetPlayerInCaptivity(false);
        AloeServerInstance.netcodeController.EnterDeathStateClientRpc(AloeServerInstance.aloeId);
        AloeServerInstance.KillEnemyServerRpc(false);
        
        AloeServerInstance.agent.speed *= 0.1f;
        AloeServerInstance.agent.acceleration = 200f;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 200f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.moveTowardsDestination = false;
        AloeServerInstance.openDoorSpeedMultiplier = 0f;
        AloeServerInstance.isEnemyDead = true;
        
        if (AloeServerInstance.roamMap.inProgress) AloeServerInstance.StopSearch(AloeServerInstance.roamMap);
    }
}