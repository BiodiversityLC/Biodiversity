using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Creatures.WaxSoldier.Misc;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Attacking)]
internal class AttackingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private bool lookAtTarget;
    private Transform lookTransform;
    
    public AttackingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Blackboard.currentAttackAction = initData.Get<AttackAction>("attackAction");
        if (EnemyAIInstance.Context.Blackboard.currentAttackAction == null)
        {
            EnemyAIInstance.LogError($"Transitioned to attack state but no 'attackAction' was given in initData.");
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
            return;
        }
        
        EnemyAIInstance.DecelerateAndStop();
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.updateRotation = false;
        
        lookAtTarget = false;
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(EnemyAIInstance.Context.Blackboard.currentAttackAction.AnimationTriggerHash);
        EnemyAIInstance.Context.Blackboard.AttackSelector.StartCooldown(EnemyAIInstance.Context.Blackboard.currentAttackAction);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.UpdateHeat();
        
        if (lookAtTarget && EnemyAIInstance.Context.Adapter.TargetPlayer)
        {
            Vector3 direction = (EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position - EnemyAIInstance.transform.position).normalized;
            direction.y = 0;
            EnemyAIInstance.transform.rotation = Quaternion.Slerp(EnemyAIInstance.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * EnemyAIInstance.Context.Blackboard.AgentAngularSpeed);
        }
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        
        EnemyAIInstance.Context.Adapter.Agent.updateRotation = true;
        EnemyAIInstance.Context.Blackboard.currentAttackAction = null;
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetHitbox.EndAttack();
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnAttackAnimationFinish):
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Pursuing);
                break;
            
            case nameof(UnmoltenAnimationHandler.OnAnimationEventStartTargetLook):
                lookAtTarget = true;
                lookTransform = eventData.Get<Transform>("lookTransform");
                break;
            
            case nameof(UnmoltenAnimationHandler.OnAnimationEventStopTargetLook):
                lookAtTarget = false;
                break;
        }
    }
}