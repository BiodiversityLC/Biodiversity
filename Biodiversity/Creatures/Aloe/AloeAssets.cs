using Biodiversity.Util.Assetloading;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

internal class AloeAssets(string bundle) : BiodiverseAssetBundle<AloeAssets>(bundle) {
#pragma warning disable 0649
    [LoadFromBundle("AloeEnemyType")] public EnemyType EnemyType;
    [LoadFromBundle("AloeTerminalNode")] public TerminalNode TerminalNode;
    [LoadFromBundle("AloeTerminalKeyword")] public TerminalKeyword TerminalKeyword;
    [LoadFromBundle("FakePlayerBodyRagdollPrefab")] public GameObject FakePlayerBodyRagdollPrefab;
#pragma warning restore 0649
}
