using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    internal class JunkRadarAssets(string bundle, bool isOptional) : BiodiverseAssetBundle<JunkRadarAssets>(bundle, isOptional)
    {
        [LoadFromBundle("Assets/Biodiversity/JunkRadar/JunkRadarItem.asset")]
        public Item JunkRadarItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/BuriedScrap.prefab")]
        public GameObject BuriedScrapPrefab;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedVaseItem.asset")]
        public Item OldVaseItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedBoardItem.asset")]
        public Item MotherboardItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedCrabItem.asset")]
        public Item CoilCrabItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedBaboonSkullItem.asset")]
        public Item BaboonSkullItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedSkullItem.asset")]
        public Item SkullItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedMugItem.asset")]
        public Item MaskedMugItem;

        [LoadFromBundle("Assets/Biodiversity/JunkRadar/Items/BuriedTrophyItem.asset")]
        public Item OgopogoTrophy;
    }
}
