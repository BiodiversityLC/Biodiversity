using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using System.Collections;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class ShootAttack : AttackAction
{
    public ShootAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsUnMolten(ctx));
        AddRequirement(ctx => ctx.Blackboard.HeldMusket.currentAmmo.Value > 0);
        AddRequirement(ctx => HasLineOfSightToTarget(ctx));
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
            Vector3 direction = ctx.Adapter.TargetPlayer.transform.position - aimTransform.position;
            direction.y = 0;
            ctx.Adapter.Transform.rotation = Quaternion.Slerp(aimTransform.rotation,
                Quaternion.LookRotation(direction), Time.deltaTime * ctx.Adapter.Agent.angularSpeed);
        }
    }

    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Finish(ctx);

        ctx.Adapter.Agent.isStopped = false;
        ctx.Adapter.Agent.updateRotation = true;
    }

    private IEnumerator AimAndShoot(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        yield return new WaitForSeconds(ctx.Blackboard.MusketAimTime);

        StopLookAtTarget();
        ctx.Blackboard.HeldMusket.SetupShotAndFire();

        yield return new WaitForSeconds(Musket.TIME_BETWEEN_FIRING_AND_BULLET_EXIT);

        ctx.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ShootMusket);
    }

    public override void HandleCustomEvent(string eventName, StateData eventData, AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.HandleCustomEvent(eventName, eventData, ctx);

        switch (eventName)
        {
            case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventStartTargetLook):
                StartLookAtTarget(eventData.Get<Transform>("aimTransform"));
                ctx.Blackboard.NetcodeController.StartCoroutine(AimAndShoot(ctx));
                break;
        }

    }

    private void StartLookAtTarget(Transform t)
    {
        aimTransform = t;
        shouldLookAtTarget = true;
    }

    private void StopLookAtTarget()
    {
        aimTransform = null;
        shouldLookAtTarget = false;
    }
}