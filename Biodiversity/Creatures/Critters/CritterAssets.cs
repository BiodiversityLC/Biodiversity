using Biodiversity.Util.Assetloading;

namespace Biodiversity.Creatures.Critters;

internal class CritterAssets(string path) : BiodiverseAssetBundle<CritterAssets>(path) {
	[LoadFromBundle("FungiEnemyType.asset")]
	public EnemyType FungiEnemyType;
}