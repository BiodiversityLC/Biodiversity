using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;

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
