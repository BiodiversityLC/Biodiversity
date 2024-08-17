using JetBrains.Annotations;

namespace Biodiversity.Creatures.Aloe;
[UsedImplicitly]
internal class AloeHandler : BiodiverseAIHandler<AloeHandler> {
    internal AloeAssets Assets { get; set; }
    internal AloeConfig Config { get; set; }

    public AloeHandler() {
        Assets = new AloeAssets("aloebracken");
        Config = new AloeConfig(BiodiversityPlugin.Instance.CreateConfig("aloe"));

        Assets.EnemyType.PowerLevel = Config.PowerLevel;
        Assets.EnemyType.MaxCount = Config.MaxAmount;
        
        TranslateTerminalNode(Assets.TerminalNode);

        RegisterEnemyWithConfig(
            Config.AloeEnabled,
            Config.Rarity,
            Assets.EnemyType,
            Assets.TerminalNode,
            Assets.TerminalKeyword);
    }
    
}