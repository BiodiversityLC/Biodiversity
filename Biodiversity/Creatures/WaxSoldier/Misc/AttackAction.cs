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
}