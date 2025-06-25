using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Dead)]
internal class DeadState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public DeadState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Adapter.Agent.speed *= 0.1f;
        EnemyAIInstance.Context.Adapter.Agent.acceleration = 200f;
        EnemyAIInstance.Context.Adapter.OpenDoorSpeedMultiplier = 0f;
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 200f;
        
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.KillEnemyServerRpc(false);
    }
    
}