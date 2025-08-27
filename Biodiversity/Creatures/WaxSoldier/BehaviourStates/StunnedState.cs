using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stunned)]
internal class StunnedState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StunnedState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionFromStunnedState(EnemyAIInstance)
        ];
    }
    
    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationControllerToFrozenClientRpc(true);
        EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ForceWalk);
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.KillAllSpeed();
        EnemyAIInstance.Context.Adapter.Agent.isStopped = true;
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.UpdateWaxDurability();
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        EnemyAIInstance.UpdatePlayerLastKnownPosition();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);
        
        EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationControllerToFrozenClientRpc(false);
        EnemyAIInstance.Context.Adapter.Agent.isStopped = false;
    }

    internal override bool OnSetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        base.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        return true; // Makes nothing happen
    }
    
    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        if (!EnemyAIInstance.Context.Adapter.ApplyDamage(force))
        {
            if (playerWhoHit)
            {
                // If the player who hit isn't our current target player that we have, then we will make them our new
                // target player IF our current target player is more than 3 units away (to prevent rapid target switcing).
                if (PlayerUtil.GetClientIdFromPlayer(playerWhoHit) != 
                    PlayerUtil.GetClientIdFromPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer) &&
                    Vector3.Distance(EnemyAIInstance.Context.Adapter.Transform.position,
                        EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position) > 3f)
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