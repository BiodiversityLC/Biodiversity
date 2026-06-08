using Biodiversity.Util;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.Critters;

[UsedImplicitly]
internal class CritterHandler : BiodiverseAIHandler<CritterHandler>
{
    internal CritterAssets Assets { get; }
    internal CritterConfig Config { get; }

    public CritterHandler()
    {
        Assets = new CritterAssets("critters");
        Config = new CritterConfig(BiodiversityPlugin.Instance.CreateConfig("critters"));

        Assets.PrototaxEnemyType.PowerLevel = Config.FungiPowerLevel;

        LethalLibUtils.TranslateTerminalNode(Assets.PrototaxTerminalNode);
        LethalLibUtils.RegisterEnemyWithConfig(
            Config.FungiEnabled,
            Config.FungiRarity,
            Assets.PrototaxEnemyType,
            Assets.PrototaxTerminalNode,
            Assets.PrototaxTerminalKeyword);

        Assets.LeafyBoiEnemyType.PowerLevel = Config.LeafBoyPowerLevel;

        LethalLibUtils.TranslateTerminalNode(Assets.LeafyBoiTerminalNode);
        LethalLibUtils.RegisterEnemyWithConfig(
            Config.LeafBoyEnabled,
            Config.LeafBoyRarity,
            Assets.LeafyBoiEnemyType,
            Assets.LeafyBoiTerminalNode,
            Assets.LeafyBoiTerminalKeyword);
    }
}