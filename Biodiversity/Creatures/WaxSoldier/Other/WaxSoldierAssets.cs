using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Creatures.WaxSoldier;

internal class WaxSoldierAssets(string bundle) : BiodiverseAssetBundle<WaxSoldierAssets>(bundle)
{
#pragma warning disable 0649
    [LoadFromBundle("WaxSoldierEnemyType")] public EnemyType EnemyType;
    [LoadFromBundle("WaxSoldierTerminalNode")] public TerminalNode TerminalNode;
    [LoadFromBundle("WaxSoldierTerminalKeyword")] public TerminalKeyword TerminalKeyword;
#pragma warning restore 0649
}