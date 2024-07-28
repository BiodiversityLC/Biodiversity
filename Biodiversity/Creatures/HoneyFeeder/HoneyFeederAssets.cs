using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Biodiversity.Creatures.HoneyFeeder;
internal class HoneyFeederAssets(string bundle) : BiodiverseAssetBundle<HoneyFeederAssets>(bundle) {
    [LoadFromBundle("HoneyFeeder.asset")]
    public EnemyType enemyType;
}
