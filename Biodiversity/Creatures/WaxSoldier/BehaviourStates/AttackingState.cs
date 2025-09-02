using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;
using GameNetcodeStuff;
using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Attacking)]
internal class AttackingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
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
        
        EnemyAIInstance.Context.Blackboard.currentAttackAction.Start(EnemyAIInstance.Context);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.Context.Adapter.MoveAgent();
        
        EnemyAIInstance.Context.Blackboard.currentAttackAction.Update(EnemyAIInstance.Context);
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        EnemyAIInstance.UpdatePlayerLastKnownPosition();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);

        EnemyAIInstance.StartCoroutine(
            EnemyAIInstance.Context.Blackboard.currentAttackAction.Finish(EnemyAIInstance.Context));
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetAttackPhysics.EndAttack();
        EnemyAIInstance.Context.Blackboard.AttackSelector.StartCooldown(EnemyAIInstance.Context.Blackboard.currentAttackAction);
    }
    
    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        // Apply the damage and do nothing else
        if (EnemyAIInstance.Context.Adapter.ApplyDamage(force))
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Dead);
        
        return true;
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        
        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnAttackAnimationFinish):
            {
                EnemyAIInstance.UpdateBehaviourStateFromPerception();
                break;
            }
            
            // For the shoot attack
            case nameof(UnmoltenAnimationHandler.OnAnimationEventStartTargetLook):
            {
                if (EnemyAIInstance.Context.Blackboard.currentAttackAction is ShootAttack shootAttackAction)
                {
                    shootAttackAction.StartLookAtTarget(eventData.Get<Transform>("aimTransform"));
                }
                
                break;
            }
            
            // For the shoot attack
            case nameof(UnmoltenAnimationHandler.OnAnimationEventMusketShoot):
            {
                if (EnemyAIInstance.Context.Blackboard.currentAttackAction is ShootAttack shootAttackAction)
                {
                    shootAttackAction.StopLookAtTarget();
                    EnemyAIInstance.Context.Blackboard.HeldMusket.SetupShoot();
                }
                
                break;
            }

            case nameof(UnmoltenAnimationHandler.OnAnimationEventStartStabAttackLunge):
            {
                EnemyAIInstance.Context.Adapter.StopAllPathing();
                
                Vector3 directionToTarget = EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position - EnemyAIInstance.Context.Adapter.Transform.position;
                directionToTarget.y = 0;
                directionToTarget.Normalize();
                
                EnemyAIInstance.Context.Adapter.Agent.velocity = directionToTarget * 15f;
                
                break;
            }
            
            case nameof(UnmoltenAnimationHandler.OnAnimationEventEndStabAttackLunge):
            {
                EnemyAIInstance.Context.Adapter.Agent.velocity = Vector3.zero;
                EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
                
                break;
            }
        }
    }
}