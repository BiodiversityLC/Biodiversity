using Biodiversity.Util;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.WaxSoldier;

[UsedImplicitly]
internal class WaxSoldierHandler : BiodiverseAIHandler<WaxSoldierHandler>
{
    internal WaxSoldierAssets Assets { get; set; }
    internal WaxSoldierConfig Config { get; set; }

    public WaxSoldierHandler()
    {
        // Assets = new WaxSoldierAssets("biodiversity_waxsoldier");
        // Config = new WaxSoldierConfig(BiodiversityPlugin.Instance.CreateConfig("waxsoldier"));
        //
        // Assets.EnemyType.PowerLevel = Config.PowerLevel;
        // Assets.EnemyType.MaxCount = Config.MaxAmount;
        //
        // LethalLibUtils.TranslateTerminalNode(Assets.TerminalNode);
        //
        // LethalLibUtils.RegisterEnemyWithConfig(
        //     Config.WaxSoldierEnabled,
        //     Config.Rarity,
        //     Assets.EnemyType,
        //     Assets.TerminalNode,
        //     Assets.TerminalKeyword);
        //
        // LethalLibUtils.RegisterScrapWithConfig("All:0", Assets.MusketItemData);
    }
}