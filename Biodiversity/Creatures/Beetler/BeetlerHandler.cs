using Biodiversity.Util.Attributes;

namespace Biodiversity.Creatures.Beetler;

[DisableEnemyByDefault]
internal class BeetlerHandler : BiodiverseAIHandler<BeetlerHandler>
{
    internal BeetlerAssets Assets { get; private set; }

    internal BeetlerConfig Config { get; private set; }

    public BeetlerHandler()
    {
        Assets = new BeetlerAssets("biodiversity_beetler");

        Config = new BeetlerConfig(BiodiversityPlugin.Instance.CreateConfig("beetler"));

        // Register butlet
        RegisterEnemyWithConfig(Config.EnableBeetler, "All:0", Assets.ButtletEnemy, Assets.ButtletTerminalNode, Assets.ButtletTerminalKeyword);
    }
}