using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.ClockworkAngel
{
    internal class ClockworkAngelAssets(string filePath) : BiodiverseAssetBundle<ClockworkAngelAssets>(filePath)
    {
        [LoadFromBundle("ClockworkAngel.prefab")]
        public GameObject enemy;

        [LoadFromBundle("AngelSpotlight.prefab")]
        public GameObject AngelSpotlight;

        [LoadFromBundle("AngelAgent.prefab")]
        public GameObject AngelAgent;
    }
}
