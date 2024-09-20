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

        TranslateTerminalNode(Assets.PrototaxTerminalNode);
        RegisterEnemyWithConfig(
            Config.PrototaxEnabled,
            Config.PrototaxRarity,
            Assets.PrototaxEnemyType,
            Assets.PrototaxTerminalNode,
            Assets.PrototaxTerminalKeyword);

        TranslateTerminalNode(Assets.LeafyBoiTerminalNode);
        RegisterEnemyWithConfig(
            Config.LeafBoyEnabled,
            Config.LeafBoyRarity,
            Assets.LeafyBoiEnemyType,
            Assets.LeafyBoiTerminalNode,
            Assets.LeafyBoiTerminalKeyword);
    }
}