using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.MovingToStation)]
internal class MovingToStationState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public MovingToStationState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToPursuitState(EnemyAIInstance),
            new TransitionToArrivingState(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        if (EnemyAIInstance.Context.Blackboard.HeldMusket.currentAmmo.Value <= 0)
        {
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Reloading);
            return;
        }

        EnemyAIInstance.Context.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PatrolMaxSpeed, WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration);
        
        EnemyAIInstance.Context.Adapter.MoveToDestination(EnemyAIInstance.Context.Blackboard.GuardPost.position);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.Context.Adapter.MoveAgent();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);

        if (transition is not TransitionToArrivingState)
        {
            EnemyAIInstance.Context.Adapter.StopAllPathing();
        }
    }
}