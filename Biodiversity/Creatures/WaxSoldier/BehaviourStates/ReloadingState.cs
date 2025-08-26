using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Reloading)]
internal class ReloadingState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private float timeSincePlayerLastSeen;
    private const float thresholdTimeWherePlayerGone = 3f;
    
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
        timeSincePlayerLastSeen = Time.time;
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.MoveWithAcceleration();
        EnemyAIInstance.UpdateWaxDurability();

        if (!hasTriggeredAnimation && EnemyAIInstance.Context.Adapter.Agent.velocity.sqrMagnitude <= 0.01f)
        {
            EnemyAIInstance.LogVerbose("Starting reload animation...");
            EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ReloadMusket);
            hasTriggeredAnimation = true;
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        if (EnemyAIInstance.UpdatePlayerLastKnownPosition())
        {
            timeSincePlayerLastSeen = Time.time;
        }
        else if (Time.time - timeSincePlayerLastSeen >= thresholdTimeWherePlayerGone)
        {
            EnemyAIInstance.Context.Adapter.TargetPlayer = null;
            EnemyAIInstance.Context.Blackboard.LastKnownPlayerPosition = default;
            EnemyAIInstance.Context.Blackboard.LastKnownPlayerVelocity = default;
        }
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        switch (eventName)
        {
            // todo: change this to an animation event because this event would get triggered even if waxy gets stunned.
            // or just make it so the animation speed goes to zero when stunned
            case nameof(UnmoltenAnimationHandler.OnReloadAnimationFinish):
            {
                EnemyAIInstance.Context.Blackboard.HeldMusket.Reload();
                
                bool playerFound = EnemyAIInstance.UpdatePlayerLastKnownPosition();
                if (playerFound)
                {
                    EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Pursuing);
                }
                else if (EnemyAIInstance.Context.Adapter.TargetPlayer)
                {
                    EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Hunting);
                }
                else
                {
                    EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
                }
                
                break;
            }
        }
    }
    
    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        // Apply the damage and do nothing else
        // todo: add config option to choose whether attacking the wax soldier interrupts the reload animation
        EnemyAIInstance.Context.Adapter.ApplyDamage(force);
        return true;
    }
}