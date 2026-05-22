using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Creatures.SwarmingLocusts
{
    internal class SwarmingLocustsAssets(string bundle) : BiodiverseAssetBundle<SwarmingLocustsAssets>(bundle)
    {
        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/SwarmingLocusts/SwarmingLocustsEnemy.asset")]
        public EnemyType SwarmingLocustsEnemy;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/SwarmingLocusts/SwarmingLocustsTK.asset")]
        public TerminalKeyword SwarmingLocustsTK;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/SwarmingLocusts/SwarmingLocustsTN.asset")]
        public TerminalNode SwarmingLocustsTN;
    }
}
