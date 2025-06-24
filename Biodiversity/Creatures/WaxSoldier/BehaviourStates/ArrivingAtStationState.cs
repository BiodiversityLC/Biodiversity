using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util.Attributes;
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
            new TransitionToStationaryState(enemyAiInstance, this)
            // todo: add TransitionToPursuitState here later on
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Blackboard.AgentMaxSpeed = 1.5f;
        EnemyAIInstance.Blackboard.AgentMaxAcceleration *= 3f; // So it can decelerate quickly

        EnemyAIInstance.Adapter.Agent.updateRotation = false;

        DesiredRotation = Quaternion.LookRotation(EnemyAIInstance.Blackboard.GuardPost.forward);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.MoveWithAcceleration();

        EnemyAIInstance.transform.rotation = Quaternion.RotateTowards(
            EnemyAIInstance.transform.rotation,
            DesiredRotation,
            100 * Time.deltaTime //todo: replace 100 with a config value for rotation speed
            );
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        
        EnemyAIInstance.Adapter.Agent.updateRotation = true;
        DesiredRotation = Quaternion.identity;
    }
}