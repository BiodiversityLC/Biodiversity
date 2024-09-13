using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Items.Developeritems;

internal class DeveloperScrapAssets(string bundle) : BiodiverseAssetBundle<DeveloperScrapAssets>(bundle)
{
#pragma warning disable 0649
    [LoadFromBundle("RubberDuckAsset")] public Item DuckAsset;
#pragma warning restore 0649
}