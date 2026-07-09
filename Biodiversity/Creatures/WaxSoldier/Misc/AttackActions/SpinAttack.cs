using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class SpinAttack : AttackAction
{
    public SpinAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsUnMolten(ctx));
    }

    public override void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Adapter.SetMovementProfile(0.2f, 50f);
        ctx.Adapter.Agent.updateRotation = false;

        Vector3 initialDirection = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        initialDirection.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.LookRotation(initialDirection);

        // We calling base here because it triggeres the animation and we wanna set the speed and rotation before doing the anim
        base.Start(ctx);

        ctx.Adapter.MoveToPlayer(ctx.Adapter.TargetPlayer);
    }

    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);

        Vector3 finalDirection = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        finalDirection.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.LookRotation(finalDirection);
        ctx.Adapter.Animator.transform.localRotation = Quaternion.identity;

        ctx.Adapter.Agent.updateRotation = true;
        ctx.Adapter.StopAllPathing();
        ctx.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PursuitMaxSpeed, WaxSoldierHandler.Instance.Config.PursuitAcceleration);
    }
}