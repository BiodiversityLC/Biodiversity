using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.SearchStrategies;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Hunting)]
internal class HuntingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private readonly SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter> searchStrategy;

    private float searchRadius = 25f;
    private float directionWeight = 1.5f;
    private float distanceWeight = 1.0f;
    private float searchTime = 30f;

    private float searchTimeLeft;
    
    public HuntingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];
        
        List<UtilityDrivenSearch.ScorerWeight> scorers =
        [
            new() { Scorer = new DirectionAlignmentScorer(EnemyAIInstance.Context), Weight = directionWeight },
            new() { Scorer = new DistanceScorer(EnemyAIInstance.Context, searchRadius), Weight = distanceWeight }
        ];
        
        searchStrategy = new UtilityDrivenSearch(EnemyAIInstance.Context, scorers, searchRadius);
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        // todo: name config values appropriately
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        
        PlayerControllerB player = EnemyAIInstance.GetClosestVisiblePlayer(
            EnemyAIInstance.Context.Adapter.EyeTransform,
            EnemyAIInstance.Context.Blackboard.ViewWidth,
            EnemyAIInstance.Context.Blackboard.ViewRange, proximityAwareness: 2f);
        if (player)
        {
            EnemyAIInstance.Context.Adapter.TargetPlayer = player;
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Pursuing);
            return;
        }
        
        // Start the search strategy and go to the prescribed position
        searchTimeLeft = searchTime;
        searchStrategy.Start();
        if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
        {
            EnemyAIInstance.LogError("No search position found; moving back guard post.");
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
            return;
        }
        
        EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
    }
    
    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateHeat();
        searchStrategy.Update();
        EnemyAIInstance.MoveWithAcceleration();
        

        searchTimeLeft -= Time.deltaTime;
        if (searchTimeLeft <= 0)
        {
            EnemyAIInstance.LogVerbose("Spent too much time searching; going back to guard post.");
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
            return;
        }
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