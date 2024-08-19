using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Creatures.Aloe.Types.Networking;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class DeadState : BehaviourState
{
    public DeadState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agent.speed *= 0.1f;
        AloeServerInstance.agent.acceleration = 200f;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 200f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.moveTowardsDestination = false;
        AloeServerInstance.openDoorSpeedMultiplier = 0f;
        AloeServerInstance.isEnemyDead = true;

        AloeServerInstance.netcodeController.AnimationParamDead.Value = true;
        
        AloeServerInstance.SetTargetPlayerInCaptivity(false);
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            AloeServerInstance.aloeId, 0, 0f);
        
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.LookTargetPosition, AloeServerInstance.GetLookAheadVector());
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.TargetPlayerClientId, AloeServer.NullPlayerId);
        
        AloeSharedData.Instance.Unbind(AloeServerInstance, BindType.Stalk);
        
        if (AloeServerInstance.roamMap.inProgress) AloeServerInstance.StopSearch(AloeServerInstance.roamMap);
        AloeServerInstance.KillEnemyServerRpc(false);
    }
}