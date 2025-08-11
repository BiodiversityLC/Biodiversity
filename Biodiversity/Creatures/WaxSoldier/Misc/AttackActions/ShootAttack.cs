using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class ShootAttack : AttackAction
{
    public ShootAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, bool requiresLineOfSight = true, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, requiresLineOfSight, priority)
    {
    }
    
    private Transform aimTransform;
    private bool shouldLookAtTarget;
    
    public override void Setup(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Setup(ctx);
        
        ctx.Adapter.StopAllPathing();
        
        ctx.Blackboard.AgentMaxAcceleration = 0f;
        ctx.Blackboard.AgentMaxSpeed = 0f;
        
        ctx.Adapter.Agent.velocity = Vector3.zero;
        ctx.Adapter.Agent.acceleration = 0f;
        ctx.Adapter.Agent.speed = 0f;
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
                Quaternion.LookRotation(direction), Time.deltaTime * ctx.Blackboard.AgentAngularSpeed);
        }
    }
    
    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);
        
        ctx.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        ctx.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;

        ctx.Adapter.Agent.isStopped = false;
        ctx.Adapter.Agent.updateRotation = true;
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