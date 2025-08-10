using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class SpinAttack : AttackAction
{
    public SpinAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, bool requiresLineOfSight = true, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, requiresLineOfSight, priority)
    {
    }

    public override void Setup(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Setup(ctx);
        
        ctx.Blackboard.AgentMaxAcceleration = 50f;
        ctx.Blackboard.AgentMaxSpeed = 1f;
        
        ctx.Adapter.Agent.updateRotation = false;
        ctx.Adapter.Agent.acceleration =
            Mathf.Min(ctx.Adapter.Agent.acceleration * 3, ctx.Blackboard.AgentMaxAcceleration);
        
        ctx.Adapter.MoveToPlayer(ctx.Adapter.TargetPlayer);
    }
    
    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);
        
        ctx.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        ctx.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        
        ctx.Adapter.Agent.updateRotation = true;
    }
}