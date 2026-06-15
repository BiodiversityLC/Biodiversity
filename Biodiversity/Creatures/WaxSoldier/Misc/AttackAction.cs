using Biodiversity.Creatures.Core;
using Biodiversity.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc;

public class AttackAction
{
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
    /// Priority of this attack. Higher values are chosen first.
    /// </summary>
    public int Priority { get; private set; }

    private readonly List<Func<AIContext<WaxSoldierBlackboard, WaxSoldierAdapter>, bool>> _requirements = [];

    public AttackAction(
        int animationTriggerHash,
        float minRange = 0f,
        float maxRange = 3f,
        float cooldown = 2f,
        int priority = 0)
    {
        AnimationTriggerHash = animationTriggerHash;
        MinRange = minRange;
        MaxRange = maxRange;
        Cooldown = cooldown;
        Priority = priority;

        AddRequirement(ctx => IsDistanceToTargetInRequiredRange(ctx));
    }

    public virtual void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        // BiodiversityPlugin.LogVerbose("In AttackAction.Setup().");
        ctx.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(AnimationTriggerHash);
    }

    public virtual void Update(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {

    }

    public virtual IEnumerator Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        yield break;
        // BiodiversityPlugin.LogVerbose("In AttackAction.Finish().");
    }

    public void AddRequirement(Func<AIContext<WaxSoldierBlackboard, WaxSoldierAdapter>, bool> requirement)
    {
        _requirements.Add(requirement);
    }

    public bool AreRequirementsMet(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        for (int i = 0; i < _requirements.Count; i++)
        {
            Func<AIContext<WaxSoldierBlackboard, WaxSoldierAdapter>, bool> requirement = _requirements[i];
            if (!requirement(ctx)) return false;
        }

        return true;
    }

    #region Frequently Used Attack Requirements
    private bool IsDistanceToTargetInRequiredRange(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        float distanceToTarget = Vector3.Distance(ctx.Adapter.Transform.position, ctx.Adapter.TargetPlayer.transform.position);
        // BiodiversityPlugin.LogVerbose($"In {nameof(IsDistanceToTargetInRequiredRange)}, distanceToTarget =  {distanceToTarget}");
        return distanceToTarget > MinRange && distanceToTarget < MaxRange;
    }

    protected bool HasLineOfSightToTarget(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        // todo: implement isFoggy properly (just get the canSeeThroughFog flag onto the adapter)
        // bool isFoggy = isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy;
        bool isFoggy = false;

        return LineOfSightUtil.HasLineOfSight(ctx.Adapter.TargetPlayer.gameplayCamera.transform.position,
            ctx.Adapter.EyeTransform, ctx.Blackboard.ViewWidth, ctx.Blackboard.ViewRange, 0.2f, isFoggy);
    }

    protected bool IsUnMolten(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        return ctx.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten;
    }

    protected bool IsMolten(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        return ctx.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Molten;
    }
    #endregion

}