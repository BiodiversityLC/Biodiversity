using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.BehaviourStates;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToStationaryState(WaxSoldierAI enemyAIInstance, ArrivingAtStationState arrivingState)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    // todo: fix this
    internal override bool ShouldTransitionBeTaken() =>
        Quaternion.Angle(EnemyAIInstance.transform.rotation, arrivingState.DesiredRotation) <= 0.1f;

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Stationary;

    internal override void OnTransition()
    {
        base.OnTransition();
        EnemyAIInstance.transform.rotation = arrivingState.DesiredRotation;
    }
}