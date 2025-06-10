using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.WalkingToStation)]
internal class WalkingToStationState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private bool reachedStation;
    
    public WalkingToStationState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToStationary(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;

        // Only call SetDestinationToPosition if its actually needed
        if (Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.PostPosition) <= 2)
        {
            reachedStation = true;
        }
        else
        {
            EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.PostPosition);
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (!reachedStation &&
            Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.PostPosition) <= 2)
        {
            reachedStation = true;
        }
        
        //todo: add logic for looking out for players that previously beefed with him
    }

    private class TransitionToStationary(
        WaxSoldierAI enemyAIInstance,
        WalkingToStationState walkingToStationState)
        : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return walkingToStationState.reachedStation;
        }

        internal override WaxSoldierAI.States NextState()
        {
            return WaxSoldierAI.States.Stationary;
        }
    }
}