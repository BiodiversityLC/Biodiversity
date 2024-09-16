using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Items.Developeritems;

internal class DeveloperScrapAssets(string bundle) : BiodiverseAssetBundle<DeveloperScrapAssets>(bundle)
{
#pragma warning disable 0649
    [LoadFromBundle("NethersomeDuckItemData")] public Item RubberDuckAsset;
#pragma warning restore 0649
}