using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax;

internal class PrototaxAssets(string bundle) : BiodiverseAssetBundle<PrototaxAssets>(bundle)
{
#pragma warning disable 0649
    [LoadFromBundle("PrototaxEnemyType")] public EnemyType EnemyType;
    [LoadFromBundle("PrototaxTerminalNode")] public TerminalNode TerminalNode;
    [LoadFromBundle("PrototaxTerminalKeyword")] public TerminalKeyword TerminalKeyword;
    [LoadFromBundle("PrototaxSporeContainer")] public GameObject SporeContainer;
#pragma warning restore 0649
}