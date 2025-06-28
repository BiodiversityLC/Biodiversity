using Biodiversity.Creatures.Core.StateMachine;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToArrivingState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        NavMeshAgent agent = EnemyAIInstance.Context.Adapter.Agent;
        
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance;
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.ArrivingAtStation;
}