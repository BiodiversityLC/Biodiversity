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

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/Items/BuriedVaseItem.asset")]
        public Item OldVaseItem;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/Items/BuriedBoardItem.asset")]
        public Item MotherboardItem;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/Items/BuriedCrabItem.asset")]
        public Item CoilCrabItem;

        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/Items/BuriedBaboonSkullItem.asset")]
        public Item BaboonSkullItem;
    }
}
