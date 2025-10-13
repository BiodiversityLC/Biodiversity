using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Items.JunkRadar
{
    internal class JunkRadarAssets(string bundle) : BiodiverseAssetBundle<JunkRadarAssets>(bundle)
    {
        [LoadFromBundle("Assets/Data/_Misc/Biodiversity/JunkRadar/JunkRadarItem.asset")]
        public Item JunkRadarItem;
    }
}
