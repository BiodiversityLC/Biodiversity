using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Biodiversity.Creatures.HoneyFeeder;
internal class HoneyFeederAssets(string bundle) : BiodiverseAssetBundle<HoneyFeederAssets>(bundle) {
    [LoadFromBundle("HoneyFeeder.asset")]
    public EnemyType enemyType;
}
