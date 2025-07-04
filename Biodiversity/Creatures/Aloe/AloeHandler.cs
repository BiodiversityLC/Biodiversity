using Biodiversity.Patches;
using Biodiversity.Util;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.Aloe;

[UsedImplicitly]
internal class AloeHandler : BiodiverseAIHandler<AloeHandler>
{
    internal AloeAssets Assets { get; set; }
    internal AloeConfig Config { get; set; }

    public AloeHandler()
    {
        Assets = new AloeAssets("aloebracken"); //todo: Load the bundle synchronously
        Config = new AloeConfig(BiodiversityPlugin.Instance.CreateConfig("aloe"));

        Assets.EnemyType.PowerLevel = Config.PowerLevel;
        Assets.EnemyType.MaxCount = Config.MaxAmount;

        if (Assets.FakePlayerBodyRagdollPrefab != null)
            GameNetworkManagerPatch.NetworkPrefabsToRegister.Add(Assets.FakePlayerBodyRagdollPrefab);
        else
            BiodiversityPlugin.Logger.LogError("FakePlayerBodyRagdollPrefab is null.");

        LethalLibUtils.TranslateTerminalNode(Assets.TerminalNode);

        LethalLibUtils.RegisterEnemyWithConfig(
            Config.AloeEnabled,
            Config.Rarity,
            Assets.EnemyType,
            Assets.TerminalNode,
            Assets.TerminalKeyword);
    }
}