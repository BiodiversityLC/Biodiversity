using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using GameNetcodeStuff;
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
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.DecelerateAndStop();
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
    
    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);
        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnReloadAnimationFinish):
            {
                EnemyAIInstance.Context.Blackboard.HeldMusket.Reload();
                EnemyAIInstance.PostAnimationLosCheck();
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