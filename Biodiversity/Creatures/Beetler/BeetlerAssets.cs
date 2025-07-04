using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;
using UnityEngine;

namespace Biodiversity.Creatures.Beetler;

#pragma warning disable CS0649

internal class BeetlerAssets(string filePath) : BiodiverseAssetBundle<BeetlerAssets>(filePath)
{
    [LoadFromBundle("Buttlet.asset")]
    public EnemyType ButtletEnemy;

    [LoadFromBundle("ButtletTN")]
    public TerminalNode ButtletTerminalNode;

    [LoadFromBundle("ButtletKW")]
    public TerminalKeyword ButtletTerminalKeyword;

    [LoadFromBundle("beetlerpartsdone.prefab")]
    public GameObject BeetlerParts;
}