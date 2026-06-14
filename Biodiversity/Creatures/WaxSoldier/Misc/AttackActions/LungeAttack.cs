namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class LungeAttack : AttackAction
{
    public LungeAttack(int animationTriggerHash, float minRange = 0, float maxRange = 3, float cooldown = 2, int priority = 0) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsMolten(ctx));
    }
}