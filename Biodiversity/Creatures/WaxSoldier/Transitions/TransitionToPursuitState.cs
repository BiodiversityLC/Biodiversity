using Biodiversity.Creatures.Core.StateMachine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToPursuitState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        bool shouldtransition = EnemyAIInstance.UpdatePlayerLastKnownPosition();
        
        if (shouldtransition)
            EnemyAIInstance.LogVerbose($"In {nameof(TransitionToPursuitState)}, saw a player.");
        
        return shouldtransition;
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Pursuing;
}