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

        EnemyAIInstance.Context.Adapter.SetMovementProfile(1.5f, 100f);
        EnemyAIInstance.Context.Adapter.Agent.updateRotation = false;

        DesiredRotation = Quaternion.LookRotation(EnemyAIInstance.Context.Blackboard.GuardPost.forward);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.Context.Adapter.MoveAgent();

        EnemyAIInstance.Context.Adapter.Transform.rotation = Quaternion.RotateTowards(
            EnemyAIInstance.Context.Adapter.Transform.rotation,
            DesiredRotation,
            EnemyAIInstance.Context.Adapter.Agent.angularSpeed * Time.deltaTime
            );
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.updateRotation = true;
        DesiredRotation = Quaternion.identity;
    }
}