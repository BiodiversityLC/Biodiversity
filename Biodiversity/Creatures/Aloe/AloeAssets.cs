using Biodiversity.Util.Assetloading;

namespace Biodiversity.Creatures.Aloe;

internal class AloeAssets(string bundle) : BiodiverseAssetBundle<AloeAssets>(bundle) {
    [LoadFromBundle("AloeEnemyType")]
    public EnemyType enemyType;
}
