using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToPursuitState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken() => EnemyAIInstance.UpdatePlayerLastKnownPosition();

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Pursuing;
}