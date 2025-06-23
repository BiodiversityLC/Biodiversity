using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stationary)]
internal class StationaryState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StationaryState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Adapter.Agent.speed = 0;
        
        EnemyAIInstance.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Blackboard.AgentMaxAcceleration = 50f;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        // todo: see the todo in the WalkingToStationState.AIIntervalBehaviour
    }
}