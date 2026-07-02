using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using System;
using System.Collections.Generic;

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

    private readonly float _minRangeSqr;
    private readonly float _maxRangeSqr;

    protected AttackAction(
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

        _minRangeSqr = minRange * minRange;
        _maxRangeSqr = maxRange * maxRange;

        AddRequirement(ctx => IsDistanceToTargetInRequiredRange(ctx));
    }

    #region Virtual Methods
    public virtual void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(AnimationTriggerHash);
    }

    public virtual void Update(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx) { }

    public virtual void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx) { }

    public virtual void HandleCustomEvent(string eventName, StateData eventData, AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx) { }
    #endregion

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
        float sqrDistance = (ctx.Adapter.TargetPlayer.transform.position - ctx.Adapter.Transform.position).sqrMagnitude;
        return sqrDistance > _minRangeSqr && sqrDistance < _maxRangeSqr;
    }

    protected bool HasLineOfSightToTarget(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        // todo: implement isFoggy properly (just get the canSeeThroughFog flag onto the adapter)
        // bool isFoggy = isOutside && !enemyType.canSeeThroughFog && TimeOfDay.Instance.currentLevelWeather == LevelWeatherType.Foggy;
        bool isFoggy = false;

        return LineOfSightUtil.HasLineOfSight(ctx.Adapter.TargetPlayer.gameplayCamera.transform.position,
            ctx.Adapter.EyeTransform, ctx.Blackboard.ViewWidth, ctx.Blackboard.ViewRange, 0.2f, isFoggy);
    }

    protected static bool IsUnMolten(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        return ctx.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Unmolten;
    }

    protected static bool IsMolten(in AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        return ctx.Blackboard.MoltenState == WaxSoldierAI.MoltenState.Molten;
    }
    #endregion
}