using Biodiversity.Creatures.Core;
using System.Collections;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class ShootAttack : AttackAction
{
    public ShootAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, bool requiresLineOfSight = true, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, requiresLineOfSight, priority)
    {
    }

    private Transform aimTransform;
    private bool shouldLookAtTarget;

    public override void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Start(ctx);

        ctx.Adapter.StopAllPathing();
        ctx.Adapter.KillAllSpeed();

        ctx.Adapter.Agent.isStopped = true;
        ctx.Adapter.Agent.updateRotation = false;
    }

    public override void Update(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Update(ctx);
        if (shouldLookAtTarget && aimTransform)
        {
            Vector3 direction = (ctx.Adapter.TargetPlayer.transform.position - aimTransform.position).normalized;
            direction.y = 0;
            ctx.Adapter.Transform.rotation = Quaternion.Slerp(aimTransform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * ctx.Adapter.Agent.angularSpeed);
        }
    }

    public override IEnumerator Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PatrolMaxSpeed, WaxSoldierHandler.Instance.Config.PatrolAcceleration);

        ctx.Adapter.Agent.isStopped = false;
        ctx.Adapter.Agent.updateRotation = true;

        yield break;
    }

    public void StartLookAtTarget(Transform t)
    {
        aimTransform = t;
        shouldLookAtTarget = true;
    }

    public void StopLookAtTarget()
    {
        shouldLookAtTarget = false;
        aimTransform = null;
    }
}