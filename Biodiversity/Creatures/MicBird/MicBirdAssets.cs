using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdAssets(string filePath) : BiodiverseAssetBundle<MicBirdAssets>(filePath)
    {
        [LoadFromBundle("MicBird.asset")]
        public EnemyType MicBirdEnemyType;

        [LoadFromBundle("MicBirdTN")]
        public TerminalNode MicBirdTerminalNode;

        [LoadFromBundle("MicBirdKW")]
        public TerminalKeyword MicBirdTerminalKeyword;
    }
}
