using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionFromStunnedState(WaxSoldierAI enemyAiInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAiInstance)
{
    internal override bool ShouldTransitionBeTaken() => EnemyAIInstance.Context.Adapter.StunNormalizedTimer <= 0;

    internal override WaxSoldierAI.States NextState() => EnemyAIInstance.PreviousState.GetStateType();
}