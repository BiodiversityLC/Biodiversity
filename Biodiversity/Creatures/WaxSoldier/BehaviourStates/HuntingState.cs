using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.SearchStrategies;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Hunting)]
internal class HuntingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private readonly SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter> searchStrategy;

    private float searchTime = 60f;
    private float searchTimeLeft;

    public HuntingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];

        // List<UtilityDrivenSearch.ScorerWeight> scorers =
        // [
        //     new() { Scorer = new DirectionAlignmentScorer(EnemyAIInstance.Context), Weight = 1.5f },
        //     new() { Scorer = new DistanceScorer(EnemyAIInstance.Context, searchRadius: 25f), Weight = 1f }
        // ];
        //
        // searchStrategy = new UtilityDrivenSearch(EnemyAIInstance.Context, scorers, searchRadius);

        searchStrategy = new PlayerBeliefFilterSearch(
            EnemyAIInstance.Context,
            // adjacencyRadius: 12f,
            // diffusionRate: 0.4f,
            // velocityBias: 2f,
            // terminationMass: 0.05f,
            // graphBuildBudgetMs: 1f,
            drawDebugField: true,
            drawDebugEdges: false
            );
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.HuntingMaxSpeed, WaxSoldierHandler.Instance.Config.HuntingAcceleration);
        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.CombatFidelityProfile);

        if (EnemyAIInstance.UpdatePlayerLastKnownPosition())
        {
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Pursuing);
            return;
        }

        // Start the search strategy and go to the prescribed position
        searchTimeLeft = searchTime;
        searchStrategy.Start();
        if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
        {
            EnemyAIInstance.LogVerbose("No search position found; moving back guard post.");
            EnemyAIInstance.SwitchBehaviourState(
                EnemyAIInstance.Context.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten ? WaxSoldierAI.States.MovingToStation : WaxSoldierAI.States.MoltenRoam);

            return;
        }

        EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.UpdateWaxDurability();
        searchStrategy.Update();
        EnemyAIInstance.Context.Adapter.MoveAgent();

        searchTimeLeft -= Time.deltaTime;
        if (searchTimeLeft <= 0)
        {
            EnemyAIInstance.LogVerbose("Search timer has finished, concluding search.");
            EnemyAIInstance.SwitchBehaviourState(
                EnemyAIInstance.Context.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten ? WaxSoldierAI.States.MovingToStation : WaxSoldierAI.States.MoltenRoam);

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
                EnemyAIInstance.LogVerbose("Search timer has finished, concluding search.");
                EnemyAIInstance.SwitchBehaviourState(
                    EnemyAIInstance.Context.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten ? WaxSoldierAI.States.MovingToStation : WaxSoldierAI.States.MoltenRoam);

                return;
            }

            EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
        }
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);

        searchStrategy.Conclude();
        EnemyAIInstance.Context.Adapter.StopAllPathing();
    }
}