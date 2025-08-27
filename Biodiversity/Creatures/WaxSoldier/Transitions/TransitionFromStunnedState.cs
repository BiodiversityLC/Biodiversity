using Biodiversity.Creatures.Core.StateMachine;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionFromStunnedState(WaxSoldierAI enemyAiInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAiInstance)
{
    internal override bool ShouldTransitionBeTaken() => EnemyAIInstance.Context.Adapter.StunNormalizedTimer <= 0;
    
    internal override WaxSoldierAI.States NextState()
    {
        WaxSoldierAI.States nextState;
                
        if (EnemyAIInstance.UpdatePlayerLastKnownPosition())
        {
            nextState = WaxSoldierAI.States.Pursuing;
        }
        else if (EnemyAIInstance.Context.Adapter.TargetPlayer && Time.time - EnemyAIInstance.Context.Blackboard.TimeWhenTargetPlayerLastSeen >= EnemyAIInstance.Context.Blackboard.ThresholdTimeWherePlayerGone.Value)
        {
            nextState = WaxSoldierAI.States.Hunting;
        }
        else
        {
            nextState = WaxSoldierAI.States.MovingToStation;
        }
        
        return nextState;
    } 
}