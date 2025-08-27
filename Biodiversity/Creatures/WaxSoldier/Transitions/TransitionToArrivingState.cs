using Biodiversity.Creatures.Core.StateMachine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToArrivingState(WaxSoldierAI enemyAiInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAiInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        NavMeshAgent agent = EnemyAIInstance.Context.Adapter.Agent;
        
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance && 
               (EnemyAIInstance.Context.Adapter.Transform.position - EnemyAIInstance.Context.Blackboard.GuardPost.position).sqrMagnitude <= 4;
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.ArrivingAtStation;
}