using Biodiversity.Util;

namespace Biodiversity.Creatures.SwarmingLocusts
{
    internal class SwarmingLocustsHandler : BiodiverseAIHandler<SwarmingLocustsHandler>
    {
        internal SwarmingLocustsAssets Assets { get; set; }
        internal SwarmingLocustsConfig Config { get; set; }

        public SwarmingLocustsHandler()
        {
            Assets = new SwarmingLocustsAssets("biodiversity_swarminglocusts");
            Config = new SwarmingLocustsConfig(BiodiversityPlugin.Instance.CreateConfig("swarming_locusts"));

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.SwarmingLocustsEnemy.enemyPrefab);
            LethalLibUtils.RegisterEnemyWithConfig(true, "All:100", Assets.SwarmingLocustsEnemy, Assets.SwarmingLocustsTN, Assets.SwarmingLocustsTK);
        }
    }
}
