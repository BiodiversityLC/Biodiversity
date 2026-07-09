using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.Search;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.SearchStrategies;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Hunting)]
internal class HuntingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private readonly SearchStrategy<WaxSoldierBlackboard, WaxSoldierAdapter> searchStrategy;
    private const float RELOAD_AFTER_SECONDS = 10f;

    private float concludeSearchTime;
    private float reloadAtTime; // The time at which the soldier should pause hunting to reload if he needs to

    private WaxSoldierAI.States nextState;
    private bool isUnmolten;
    private bool doesMusketNeedReloading;

    public HuntingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];

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

        isUnmolten = EnemyAIInstance.Context.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten;
        doesMusketNeedReloading = EnemyAIInstance.Context.Blackboard.HeldMusket.currentAmmo.Value <= 0;
        nextState = isUnmolten ? WaxSoldierAI.States.MovingToStation : WaxSoldierAI.States.MoltenRoam;

        // Start the search strategy and go to the prescribed position
        searchStrategy.Start();
        if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
        {
            EnemyAIInstance.LogVerbose("No search position found; moving back guard post.");
            EnemyAIInstance.SwitchBehaviourState(nextState);
            return;
        }

        // Set the search timers
        concludeSearchTime = Time.time + EnemyAIInstance.Context.Blackboard.HuntingLingerTime;
        reloadAtTime = Time.time + RELOAD_AFTER_SECONDS;

        EnemyAIInstance.Context.Adapter.MoveToDestination(searchPosition);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.UpdateWaxDurability();
        searchStrategy.Update();
        EnemyAIInstance.Context.Adapter.MoveAgent();
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (Time.time >= concludeSearchTime)
        {
            EnemyAIInstance.LogVerbose("Search timer has finished; concluding hunt.");
            EnemyAIInstance.SwitchBehaviourState(nextState);
            return;
        }
        if (isUnmolten && doesMusketNeedReloading && Time.time >= reloadAtTime)
        {
            EnemyAIInstance.LogVerbose("Temporarily pausing hunt to reload...");
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Reloading);
            return;
        }

        if (EnemyAIInstance.Context.Adapter.HasReachedDestination())
        {
            if (!searchStrategy.TryGetNextSearchPosition(out Vector3 searchPosition))
            {
                EnemyAIInstance.LogVerbose("No more places to search; concluding hunt.");
                EnemyAIInstance.SwitchBehaviourState(nextState);

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