using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Creatures.SwarmingLocusts
{
    internal class SwarmingLocustsAssets(string bundle) : BiodiverseAssetBundle<SwarmingLocustsAssets>(bundle)
    {
        [LoadFromBundle("Assets/Biodiversity/SwarmingLocusts/SwarmingLocustsEnemy.asset")]
        public EnemyType SwarmingLocustsEnemy;

        [LoadFromBundle("Assets/Biodiversity/SwarmingLocusts/SwarmingLocustsTK.asset")]
        public TerminalKeyword SwarmingLocustsTK;

        [LoadFromBundle("Assets/Biodiversity/SwarmingLocusts/SwarmingLocustsTN.asset")]
        public TerminalNode SwarmingLocustsTN;
    }
}
