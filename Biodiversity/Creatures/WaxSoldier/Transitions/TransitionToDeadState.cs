using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToDeadState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken() => EnemyAIInstance.Context.Adapter.IsDead;

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Dead;
}