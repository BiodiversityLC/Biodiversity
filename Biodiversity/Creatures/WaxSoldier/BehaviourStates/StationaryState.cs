using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stationary)]
internal class StationaryState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StationaryState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.DecelerateAndStop();
        
        EnemyAIInstance.Context.Blackboard.NetcodeController.AnimationParamInSalute.Set(true);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.UpdateWaxDurability();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);
        EnemyAIInstance.Context.Blackboard.NetcodeController.AnimationParamInSalute.Set(false);
    }
}