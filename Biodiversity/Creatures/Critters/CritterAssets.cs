using Biodiversity.Util.Assetloading;
#pragma warning disable CS0649 // Fields are filled in with reflection.

namespace Biodiversity.Creatures.Critters;

internal class CritterAssets(string path) : BiodiverseAssetBundle<CritterAssets>(path) {
	[LoadFromBundle("PrototaxEnemyType.asset")]
	public EnemyType PrototaxEnemyType;

	[LoadFromBundle("PrototaxTerminalKeyword")]
	public TerminalKeyword PrototaxTerminalKeyword;

	[LoadFromBundle("PrototaxTerminalNode")]
	public TerminalNode PrototaxTerminalNode;
	
	[LoadFromBundle("LeafBoiEnemyType.asset")]
	public EnemyType LeafyBoiEnemyType;

	[LoadFromBundle("LeafyBoiTerminalKeyword")]
	public TerminalKeyword LeafyBoiTerminalKeyword;

	[LoadFromBundle("LeafyBoiTerminalNode")]
	public TerminalNode LeafyBoiTerminalNode;
}