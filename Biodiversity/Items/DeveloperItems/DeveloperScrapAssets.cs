using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Items.Developeritems;

internal class DeveloperScrapAssets(string bundle) : BiodiverseAssetBundle<DeveloperScrapAssets>(bundle)
{
#pragma warning disable 0649
    [LoadFromBundle("NethersomeDuckItemData")] public Item RubberDuckAsset;
    [LoadFromBundle("AutumnisIronDogItemData")] public Item IronDogAsset;
    [LoadFromBundle("CDogItemData")] public Item CDogAsset;
    [LoadFromBundle("JacuPlushieItemData")] public Item JacuPlushieAsset;
#pragma warning restore 0649
}