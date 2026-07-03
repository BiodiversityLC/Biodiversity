using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using GameNetcodeStuff;
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

        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.CombatFidelityProfile);
        EnemyAIInstance.Context.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PursuitMaxSpeed, WaxSoldierHandler.Instance.Config.PursuitAcceleration);

        EnemyAIInstance.Context.Blackboard.CurrentAttackAction.Start(EnemyAIInstance.Context);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        EnemyAIInstance.UpdateWaxDurability();
        EnemyAIInstance.Context.Adapter.MoveAgent();

        EnemyAIInstance.Context.Blackboard.CurrentAttackAction.Update(EnemyAIInstance.Context);
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        EnemyAIInstance.UpdatePlayerLastKnownPosition();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);

        EnemyAIInstance.Context.Blackboard.CurrentAttackAction.Finish(EnemyAIInstance.Context);
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetAttackPhysics.EndAttack();
        EnemyAIInstance.StartAttackCooldown(EnemyAIInstance.Context.Blackboard.CurrentAttackAction);

        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.PatrolFidelityProfile);
    }

    internal override bool OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);

        StateData data = new();
        data.Add("playerCollider", other);
        EnemyAIInstance.Context.Blackboard.CurrentAttackAction.HandleCustomEvent(nameof(OnCollideWithPlayer), data, EnemyAIInstance.Context);

        return true;
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

        EnemyAIInstance.Context.Blackboard.CurrentAttackAction.HandleCustomEvent(eventName, eventData, EnemyAIInstance.Context);

        switch (eventName)
        {
            case nameof(WaxSoldierAnimationEventHandler.OnAttackAnimationFinish):
            {
                EnemyAIInstance.UpdateBehaviourStateFromPerception();
                break;
            }
        }
    }
}