using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;
using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab;

#pragma warning disable CS0649

internal class CoilCrabAssets(string filePath) : BiodiverseAssetBundle<CoilCrabAssets>(filePath)
{
    [LoadFromBundle("CoilShell.asset")]
    public Item CoilShellItem;

    [LoadFromBundle("CoilShell.prefab")]
    public GameObject CoilShell;

    [LoadFromBundle("CoilCrab.asset")]
    public EnemyType CoilCrabEnemy;

    [LoadFromBundle("CoilCrabTN")]
    public TerminalNode CoilCrabTerminalNode;

    [LoadFromBundle("CoilCrabKW")]
    public TerminalKeyword CoilCrabTerminalKeyword;
}