using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.BehaviourStates;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToStationaryState(WaxSoldierAI enemyAIInstance, ArrivingAtStationState arrivingState)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken() => EnemyAIInstance.Context.Adapter.HasReachedDestination();

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Stationary;

    internal override void OnTransition()
    {
        base.OnTransition();

        EnemyAIInstance.transform.rotation = arrivingState.DesiredRotation;
    }
}