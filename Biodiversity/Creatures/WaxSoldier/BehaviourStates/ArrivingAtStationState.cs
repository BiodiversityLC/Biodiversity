using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.ArrivingAtStation)]
internal class ArrivingAtStationState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    internal Quaternion DesiredRotation { get; private set; }
    
    public ArrivingAtStationState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToPursuitState(EnemyAIInstance),
            new TransitionToStationaryState(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 1.5f;
        EnemyAIInstance.Context.Adapter.Agent.acceleration *= 3f; // So it can decelerate quickly

        EnemyAIInstance.Context.Adapter.Agent.updateRotation = false;

        DesiredRotation = Quaternion.LookRotation(EnemyAIInstance.Context.Blackboard.GuardPost.forward);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.MoveWithAcceleration();

        EnemyAIInstance.transform.rotation = Quaternion.RotateTowards(
            EnemyAIInstance.transform.rotation,
            DesiredRotation,
            EnemyAIInstance.Context.Blackboard.AgentAngularSpeed * Time.deltaTime
            );
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        
        EnemyAIInstance.Context.Adapter.Agent.updateRotation = true;
        DesiredRotation = Quaternion.identity;
    }
}