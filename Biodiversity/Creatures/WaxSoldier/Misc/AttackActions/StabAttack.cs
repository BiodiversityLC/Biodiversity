namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class StabAttack : AttackAction
{
    public StabAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsUnMolten(ctx));
        AddRequirement(ctx => HasLineOfSightToTarget(ctx));
    }
}