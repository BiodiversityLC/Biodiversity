using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Creatures.Rock;

#pragma warning disable CS0649

internal class RockAssets(string filePath) : BiodiverseAssetBundle<RockAssets>(filePath)
{
    [LoadFromBundle("Rock.asset")]
    public EnemyType RockEnemyType;
}