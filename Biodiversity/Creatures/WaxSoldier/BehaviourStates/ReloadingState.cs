using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Reloading)]
internal class ReloadingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public ReloadingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    private bool hasTriggeredAnimation;

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        hasTriggeredAnimation = false;
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.DecelerateAndStop();
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();

        if (!hasTriggeredAnimation && EnemyAIInstance.Context.Adapter.Agent.velocity.sqrMagnitude <= 0.01f)
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ReloadMusket);
    }
    
    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnReloadAnimationFinish):
                EnemyAIInstance.Context.Blackboard.HeldMusket.Reload();
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
                break;
        }
    }
}