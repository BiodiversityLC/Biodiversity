using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Util;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Reloading)]
internal class ReloadingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private bool hasTriggeredAnimation;
    
    public ReloadingState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }
    
    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.DecelerateAndStop();
        
        hasTriggeredAnimation = false;
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.MoveWithAcceleration();
        EnemyAIInstance.UpdateWaxDurability();

        if (!hasTriggeredAnimation && EnemyAIInstance.Context.Adapter.Agent.velocity.sqrMagnitude <= 0.6f)
        {
            EnemyAIInstance.LogVerbose("Starting reload animation...");
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ReloadMusket);
            hasTriggeredAnimation = true;
            EnemyAIInstance.KillAllSpeed();
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        EnemyAIInstance.UpdatePlayerLastKnownPosition();
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        
        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnReloadAnimationFinish):
            {
                EnemyAIInstance.Context.Blackboard.HeldMusket.Reload();
                EnemyAIInstance.Bacalhau();
                break;
            }
        }
    }
    
    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        // todo: add config option to choose whether attacking the wax soldier interrupts the reload animation
        
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