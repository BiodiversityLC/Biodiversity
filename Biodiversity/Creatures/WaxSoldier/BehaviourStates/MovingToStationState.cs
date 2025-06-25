using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.MovingToStation)]
internal class MovingToStationState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public MovingToStationState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToArrivingState(EnemyAIInstance)
            // todo: add TransitionToPursuitState here later on
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        //todo: name config values appropriately
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        EnemyAIInstance.Context.Adapter.OpenDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        EnemyAIInstance.Context.Adapter.MoveToDestination(EnemyAIInstance.Context.Blackboard.GuardPost.position);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.MoveWithAcceleration();
    }
}