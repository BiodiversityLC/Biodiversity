using Biodiversity.Util.Assetloading;

namespace Biodiversity.Creatures.Ogopogo;

class OgopogoAssets(string filePath) : BiodiverseAssetBundle<OgopogoAssets>(filePath) {
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
}