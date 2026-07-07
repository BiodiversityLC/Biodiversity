using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class SwingAttack : AttackAction
{
    public SwingAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsMolten(ctx));
    }

    public override void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Adapter.BeginGracefulStop();
        ctx.Adapter.StopAllPathing();
        ctx.Adapter.Agent.updateRotation = false;

        Vector3 initialDirection = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        initialDirection.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.LookRotation(initialDirection);

        base.Start(ctx);
    }

    public override void AIInterval(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.AIInterval(ctx);

        Vector3 direction = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        direction.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.Slerp(ctx.Adapter.Transform.rotation,
            Quaternion.LookRotation(direction), Time.deltaTime * ctx.Adapter.Agent.angularSpeed);
    }

    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);

        ctx.Adapter.Agent.updateRotation = true;
        ctx.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PursuitMaxSpeed, WaxSoldierHandler.Instance.Config.PursuitAcceleration);
    }
}