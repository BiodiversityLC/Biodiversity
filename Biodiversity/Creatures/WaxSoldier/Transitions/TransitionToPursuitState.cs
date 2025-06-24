using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToPursuitState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        return EnemyAIInstance.IsAPlayerInLineOfSightToEye(
            EnemyAIInstance.Adapter.EyeTransform,
            EnemyAIInstance.Blackboard.ViewWidth,
            EnemyAIInstance.Blackboard.ViewRange
            );
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Pursuing;
}