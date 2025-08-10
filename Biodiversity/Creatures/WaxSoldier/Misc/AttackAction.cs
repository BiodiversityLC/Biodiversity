using Biodiversity.Creatures.Core;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc;

public class AttackAction
{
    // /// <summary>
    // /// The type of attack, used by the hitbox to determine damage/effects.
    // /// </summary>
    // public BayonetHitbox.AttackType AttackType;

    /// <summary>
    /// Hash of the name of the animation trigger to play for this attack.
    /// </summary>
    public int AnimationTriggerHash { get; private set; }

    /// <summary>
    /// The minimum range to consider this attack.
    /// </summary>
    public float MinRange { get; private set; }

    /// <summary>
    /// The maximum range to consider this attack.
    /// </summary>
    public float MaxRange { get; private set; }

    /// <summary>
    /// Seconds to wait before this specific attack can be used again.
    /// </summary>
    public float Cooldown { get; private set; }

    /// <summary>
    /// Whether this attack requires a direct line of sight.
    /// </summary>
    public bool RequiresLineOfSight { get; private set; }

    /// <summary>
    /// Priority of this attack. Higher values are chosen first.
    /// </summary>
    public int Priority { get; private set; }

    public AttackAction(
        int animationTriggerHash,
        float minRange = 0f, 
        float maxRange = 3f,
        float cooldown = 2f,
        bool requiresLineOfSight = true,
        int priority = 0)
    {
        AnimationTriggerHash = animationTriggerHash;
        MinRange = minRange;
        MaxRange = maxRange;
        Cooldown = cooldown;
        RequiresLineOfSight = requiresLineOfSight;
        Priority = priority;
    }

    private Transform aimTransform;
    private bool shouldLookAtTarget;

    public virtual void Setup(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        BiodiversityPlugin.LogVerbose("In AttackAction.Setup().");
        StopLookAtTarget();
        ctx.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(AnimationTriggerHash);
    }

    public virtual void Update(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        if (shouldLookAtTarget && aimTransform)
        {
            Vector3 direction = (ctx.Adapter.TargetPlayer.transform.position - aimTransform.position).normalized;
            direction.y = 0;
            ctx.Adapter.Transform.rotation = Quaternion.Slerp(aimTransform.rotation, 
                Quaternion.LookRotation(direction), Time.deltaTime * ctx.Blackboard.AgentAngularSpeed);
        }
    }

    public virtual void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        BiodiversityPlugin.LogVerbose("In AttackAction.Finish().");
        StopLookAtTarget();
    }

    // todo: move these over to ShootAttack.cs
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