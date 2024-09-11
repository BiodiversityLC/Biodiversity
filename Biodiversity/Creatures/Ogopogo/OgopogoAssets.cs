using Biodiversity.Util.Assetloading;
using Biodiversity.Util.Attributes;

namespace Biodiversity.Creatures.Ogopogo;

internal class OgopogoAssets(string filePath) : BiodiverseAssetBundle<OgopogoAssets>(filePath) {
#pragma	warning disable 0649
	[LoadFromBundle("Ogopogo.asset")]
	public EnemyType OgopogoEnemyType;

	[LoadFromBundle("OgopogoTN")]
	public TerminalNode OgopogoTerminalNode;

	[LoadFromBundle("OgopogoKW")]
	public TerminalKeyword OgopogoTerminalKeyword;
	
	[LoadFromBundle("Vermin.asset")]
	public EnemyType VerminEnemyType;

	[LoadFromBundle("VerminTN")]
	public TerminalNode VerminTerminalNode;

	[LoadFromBundle("VerminKW")]
	public TerminalKeyword VerminTerminalKeyword;
#pragma	warning restore 0649	
}