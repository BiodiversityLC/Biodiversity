using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LethalLib.Modules;

namespace Biodiversity.Items.Developeritems
{
    internal class DeveloperScarpAssets(string Bundle) : BiodiverseAssetBundle<DeveloperScarpAssets>(Bundle)
    {
        [LoadFromBundle("RubberDuckAsset")]
        public Item DuckAsset;
    }
}
