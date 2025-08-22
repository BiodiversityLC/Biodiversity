using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stunned)]
internal class StunnedState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StunnedState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionFromStunnedState(EnemyAIInstance)
        ];
    }
    
    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.speed = 0;
        EnemyAIInstance.Context.Adapter.Agent.velocity = Vector3.zero;
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
        
        // play stun animation
    }
}