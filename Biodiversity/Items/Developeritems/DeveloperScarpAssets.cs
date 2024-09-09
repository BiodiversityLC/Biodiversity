using Biodiversity.Util.Assetloading;

namespace Biodiversity.Items.Developeritems
{
    internal class DeveloperScarpAssets(string Bundle) : BiodiverseAssetBundle<DeveloperScarpAssets>(Bundle)
    {
        [LoadFromBundle("RubberDuckAsset")]
        public Item DuckAsset;
    }
}
