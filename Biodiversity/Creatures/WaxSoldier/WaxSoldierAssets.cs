using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Creatures.WaxSoldier
{
    internal class WaxSoldierAssets(string bundle) : BiodiverseAssetBundle<WaxSoldierAssets>(bundle)
    {
        [LoadFromBundle("WaxSoldierType")]
        public EnemyType WaxSoldierType;
        [LoadFromBundle("WaxSoldierNode")]
        public TerminalNode WaxSoldierNode;
        [LoadFromBundle("WaxSoldierKey")]
        public TerminalKeyword WaxSoldierKey;
    }
}
