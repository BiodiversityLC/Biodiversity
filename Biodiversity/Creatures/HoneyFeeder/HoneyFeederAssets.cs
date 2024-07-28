using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;
internal class HoneyFeederAssets(string bundle) : BiodiverseAssetBundle<HoneyFeederAssets>(bundle) {
    [LoadFromBundle("HoneyFeeder.asset")]
    public EnemyType enemyType;
}
