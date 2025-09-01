using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Util;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Pursuing)]
internal class PursuitState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public PursuitState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        // todo: name config values appropriately
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        EnemyAIInstance.Context.Blackboard.ThresholdTimeWherePlayerGone.Override(1.5f);
        
        EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.MoveWithAcceleration();
    }
    
    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        if (EnemyAIInstance.UpdatePlayerLastKnownPosition())
        {
            AttackAction selectedAttack =
                EnemyAIInstance.Context.Blackboard.AttackSelector.SelectAttack(EnemyAIInstance, EnemyAIInstance.Context.Adapter.TargetPlayer);
        
            if (selectedAttack != null)
            {
                StateData data = new();
                data.Add("attackAction", selectedAttack);
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Attacking, initData: data);
            }
        }
        else if (Time.time - EnemyAIInstance.Context.Blackboard.TimeWhenTargetPlayerLastSeen < EnemyAIInstance.Context.Blackboard.ThresholdTimeWherePlayerGone.Value)
        {
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Hunting);
        }
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Blackboard.ThresholdTimeWherePlayerGone.UseDefault();
    }

    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        if (!EnemyAIInstance.Context.Adapter.ApplyDamage(force))
        {
            if (playerWhoHit)
            {
                // If the player who hit isn't our current target player that we are chasing, then we will make them our new
                // target player IF our current target player is more than 6 units away (to prevent rapid target switcing).
                if (PlayerUtil.GetClientIdFromPlayer(playerWhoHit) != 
                    PlayerUtil.GetClientIdFromPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer) &&
                    Vector3.Distance(EnemyAIInstance.Context.Adapter.Transform.position,
                        EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position) > 6f)
                {
                    EnemyAIInstance.Context.Adapter.MoveToPlayer(playerWhoHit);

                    EnemyAIInstance.Context.Adapter.TargetPlayer = playerWhoHit;
                    EnemyAIInstance.Context.Blackboard.LastKnownPlayerPosition = playerWhoHit.transform.position;
                    EnemyAIInstance.Context.Blackboard.LastKnownPlayerVelocity = PlayerUtil.GetVelocityOfPlayer(playerWhoHit);
                    EnemyAIInstance.Context.Blackboard.TimeWhenTargetPlayerLastSeen = Time.time;
                }
            }
        }
        else
        {
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Dead);
        }
        
        return true;
    }
}