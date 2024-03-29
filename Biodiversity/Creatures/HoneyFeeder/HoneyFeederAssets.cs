using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;
internal class HoneyFeederAssets() : BiodiverseAssetBundle<HoneyFeederAssets>("biodiversity_honeyfeeder") {
    [LoadFromBundle("HoneyFeeder.asset")]
    public EnemyType enemyType;
}
