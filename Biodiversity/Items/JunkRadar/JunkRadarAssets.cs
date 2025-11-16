using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    internal class JunkRadarAssets(string bundle) : BiodiverseAssetBundle<JunkRadarAssets>(bundle)
    {
        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/JunkRadarItem.asset")]
        public Item JunkRadarItem;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/BuriedScrap.prefab")]
        public GameObject BuriedScrapPrefab;
    }
}
