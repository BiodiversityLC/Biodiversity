using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToPursuitState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        return EnemyAIInstance.IsAPlayerInLineOfSightToEye(
            EnemyAIInstance.Context.Adapter.EyeTransform,
            EnemyAIInstance.Context.Blackboard.ViewWidth,
            EnemyAIInstance.Context.Blackboard.ViewRange
            );
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Pursuing;
}