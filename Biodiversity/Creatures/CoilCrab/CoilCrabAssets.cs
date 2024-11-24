using Biodiversity.Creatures.MicBird;
using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilCrabAssets(string filePath) : BiodiverseAssetBundle<CoilCrabAssets>(filePath)
    {
        [LoadFromBundle("CoilShell.asset")]
        public Item CoilShellItem;

        [LoadFromBundle("CoilShell.prefab")]
        public GameObject CoilShell;

        [LoadFromBundle("CoilCrab.asset")]
        public EnemyType CoilCrabEnemy;

        [LoadFromBundle("CoilCrabTN")]
        public TerminalNode CoilCrabTerminalNode;

        [LoadFromBundle("CoilCrabKW")]
        public TerminalKeyword CoilCrabTerminalKeyword;
    }
}
