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
        
        EnemyAIInstance.Adapter.Agent.speed *= 0.1f;
        EnemyAIInstance.Adapter.Agent.acceleration = 200f;
        EnemyAIInstance.Adapter.OpenDoorSpeedMultiplier = 0f;
        EnemyAIInstance.Adapter.StopAllPathing();
        
        EnemyAIInstance.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Blackboard.AgentMaxAcceleration = 200f;
        
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.KillEnemyServerRpc(false);
    }
    
}