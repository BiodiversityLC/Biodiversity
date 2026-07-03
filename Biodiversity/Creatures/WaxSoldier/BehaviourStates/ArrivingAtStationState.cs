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

        // We project the forward direction of the guard post node onto the XZ plane
        Vector3 planarForward = Vector3.ProjectOnPlane(EnemyAIInstance.Context.Blackboard.GuardPost.forward, Vector3.up);

        // We check the magnitude because if the guard post forward is equal to Vector3.up,
        // then when it gets projected onto the XZ plane we get Vector3.zero,
        // which is not gonna work with Quaternion.LookRotation
        DesiredRotation = planarForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(planarForward.normalized)
            : EnemyAIInstance.Context.Adapter.Transform.rotation;
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