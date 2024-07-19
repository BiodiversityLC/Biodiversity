using Biodiversity.Util.Assetloading;

namespace Biodiversity.Creatures.Critters;

internal class CritterAssets(string path) : BiodiverseAssetBundle<CritterAssets>(path) {
	[LoadFromBundle("PrototaxEnemyType.asset")]
	public EnemyType PrototaxEnemyType;
}