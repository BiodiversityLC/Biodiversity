using Biodiversity.Util.Assetloading;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

internal class AloeAssets(string bundle) : BiodiverseAssetBundle<AloeAssets>(bundle) {
    [LoadFromBundle("AloeEnemyType")]
    public EnemyType enemyType;

    [LoadFromBundle("FakePlayerBodyRagdollPrefab")]
    public GameObject fakePlayerBodyRagdollPrefab;
}
