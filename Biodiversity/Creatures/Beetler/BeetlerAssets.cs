using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.Beetler
{
    internal class BeetlerAssets(string filePath) : BiodiverseAssetBundle<BeetlerAssets>(filePath)
    {
        [LoadFromBundle("Buttlet.asset")]
        public EnemyType ButtletEnemy;

        [LoadFromBundle("ButtletTN")]
        public TerminalNode ButtletTerminalNode;

        [LoadFromBundle("ButtletKW")]
        public TerminalKeyword ButtletTerminalKeyword;

        [LoadFromBundle("beetlerpartsdone.prefab")]
        public GameObject BeetlerParts;
    }
}
