using Biodiversity.Creatures.Core;
using System.Collections;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class SpinAttack : AttackAction
{
    public SpinAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, bool requiresLineOfSight = true, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, requiresLineOfSight, priority)
    {
    }

    public override void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Adapter.SetMovementProfile(1f, 50f);
        ctx.Adapter.Agent.updateRotation = false;

        Vector3 initialDirection = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        initialDirection.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.LookRotation(initialDirection);
        
        // todo: make the whole attack action thing not poopoo. We calling base here because it triggeres the animation and we wanna set the speed and rotation before doing the anim
        base.Start(ctx);
        
        ctx.Adapter.MoveToPlayer(ctx.Adapter.TargetPlayer);
    }
    
    public override IEnumerator Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);
        
        Vector3 finalDirection = ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position;
        finalDirection.y = 0;
        ctx.Adapter.Transform.rotation = Quaternion.LookRotation(finalDirection);

        yield return null;
        ctx.Adapter.Animator.gameObject.transform.localRotation = Quaternion.identity;
        
        ctx.Adapter.Agent.updateRotation = true;
        ctx.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.PatrolMaxSpeed, WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration);
    }
}