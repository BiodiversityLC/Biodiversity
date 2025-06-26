using Biodiversity.Creatures.Core.Search;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.SearchStrategies;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util.Attributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Hunting)]
internal class HuntingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private readonly ISearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter> searchStrategy = new PlayerVectorNodeSearch();
    
    public HuntingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        // Initialize the search strategy and go to the prescribed position
        searchStrategy.Initialize(EnemyAIInstance.Context);
        if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
        {
            EnemyAIInstance.LogError("No search position found.");
            return;
            // todo: add fallback option or whatever
        }
        
        EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
    }
    
    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.MoveWithAcceleration();
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (EnemyAIInstance.Context.Adapter.HasReachedDestination())
        {
            if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
            {
                EnemyAIInstance.LogVerbose("No more places to search; going back to the guard post.");
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
                return;
            }
            
            EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
        }
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        searchStrategy.Conclude();
    }
}