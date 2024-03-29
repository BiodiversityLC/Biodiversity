using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;
internal class WaxSoldierAssets() : BiodiverseAssetBundle<WaxSoldierAssets>("") //No assets for Wax Soldier yet
{
    //[LoadFromBundle("")]
    public EnemyType enemyType;
}