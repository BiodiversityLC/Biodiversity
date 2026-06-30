using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class StabAttack : AttackAction
{
    public StabAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsUnMolten(ctx));
        AddRequirement(ctx => HasLineOfSightToTarget(ctx));
    }

    public override void HandleAnimationEvent(string eventName, StateData eventData, AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.HandleAnimationEvent(eventName, eventData, ctx);

        switch (eventName)
        {
            case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventStartStabAttackLunge):
                ctx.Adapter.StopAllPathing();

                Vector3 directionToTarget =ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
                directionToTarget.y = 0;
                directionToTarget.Normalize();

                ctx.Adapter.Agent.velocity = directionToTarget * 15f;

                break;

            case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventEndStabAttackLunge):
                ctx.Adapter.Agent.velocity = Vector3.zero;
                ctx.Adapter.MoveToPlayer(ctx.Adapter.TargetPlayer);

                break;
        }
    }
}