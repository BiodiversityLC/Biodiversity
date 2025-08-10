using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class ShootAttack : AttackAction
{
    public ShootAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, bool requiresLineOfSight = true, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, requiresLineOfSight, priority)
    {
    }
    
    public override void Setup(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Setup(ctx);
        
        ctx.Blackboard.AgentMaxAcceleration = 0f;
        ctx.Blackboard.AgentMaxSpeed = 0f;
        
        ctx.Adapter.Agent.updateRotation = false;
        ctx.Adapter.Agent.acceleration = 0f;
        ctx.Adapter.Agent.speed = 0f;
        
        ctx.Adapter.StopAllPathing();
    }
    
    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);
        
        ctx.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        ctx.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        
        ctx.Adapter.Agent.updateRotation = true;
    }
}