using Biodiversity.Util.Assetloading;

namespace Biodiversity.Creatures.Aloe;

internal class AloeAssets() : BiodiverseAssetBundle<AloeAssets>("aloebracken")
{
    [LoadFromBundle("AloeEnemyType")] public EnemyType enemyType;
}