using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Creatures.Critters;

internal class CritterAssets(string path) : BiodiverseAssetBundle<CritterAssets>(path)
{
#pragma warning disable 0649
    [LoadFromBundle("PrototaxEnemyType.asset")] public EnemyType PrototaxEnemyType;
    [LoadFromBundle("PrototaxTerminalKeyword")] public TerminalKeyword PrototaxTerminalKeyword;
    [LoadFromBundle("PrototaxTerminalNode")] public TerminalNode PrototaxTerminalNode;
    
    [LoadFromBundle("LeafBoiEnemyType.asset")] public EnemyType LeafyBoiEnemyType;
    [LoadFromBundle("LeafyBoiTerminalKeyword")] public TerminalKeyword LeafyBoiTerminalKeyword;
    [LoadFromBundle("LeafyBoiTerminalNode")] public TerminalNode LeafyBoiTerminalNode;
#pragma warning restore 0649
}