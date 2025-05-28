using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierServerAI.States.WalkingToStation)]
internal class WalkingToStationState : BehaviourState<WaxSoldierServerAI.States, WaxSoldierServerAI>
{
    private bool reachedStation;
    
    public WalkingToStationState(WaxSoldierServerAI enemyAiInstance) : base(enemyAiInstance)
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
        EnemyAIInstance.openDoorSpeedMultiplier = 2f; //todo: make config for this
        EnemyAIInstance.moveTowardsDestination = true;

        // Only call SetDestinationToPosition if its actually needed
        if (Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.StationPosition) <= 2)
            reachedStation = true;
        else 
            EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.StationPosition);
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (!reachedStation &&
            Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.StationPosition) <= 2)
        {
            reachedStation = true;
        }
        
        //todo: add logic for looking out for players that previously beefed with him
    }

    private class TransitionToStationary(
        WaxSoldierServerAI enemyAIInstance,
        WalkingToStationState walkingToStationState)
        : StateTransition<WaxSoldierServerAI.States, WaxSoldierServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return walkingToStationState.reachedStation;
        }

        internal override WaxSoldierServerAI.States NextState()
        {
            return WaxSoldierServerAI.States.Stationary;
        }
    }
}