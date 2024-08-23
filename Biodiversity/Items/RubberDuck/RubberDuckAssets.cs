using Biodiversity.Util.Assetloading;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using LethalLib.Modules;


namespace Biodiversity.Items.RubberDuck
{
    internal class RubberDuckAssets(string Bundle) : BiodiverseAssetBundle<RubberDuckAssets>(Bundle)
    {
        [LoadFromBundle("RubberDuckAsset")] 
        public Item DuckAsset;
    }
}
