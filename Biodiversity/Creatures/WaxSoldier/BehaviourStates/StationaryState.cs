using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stationary)]
internal class StationaryState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StationaryState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToPursuitState(enemyAiInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.speed = 0;
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
    }
}