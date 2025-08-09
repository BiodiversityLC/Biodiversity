using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.TransformingToMolten)]
internal class TransformingToMoltenState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public TransformingToMoltenState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        EnemyAIInstance.Context.Blackboard.MoltenState = WaxSoldierAI.MoltenState.Molten;
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.speed = 0;
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
    }
}