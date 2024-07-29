using Biodiversity.Creatures.Critters;
using Biodiversity.General;
using LethalLib.Modules;

namespace Biodiversity.Creatures.Ogopogo;

class OgopogoHandler : BiodiverseAIHandler<OgopogoHandler> {
	internal OgopogoAssets Assets { get; private set; }

	internal OgopogoConfig Config { get; private set; }
    
	public OgopogoHandler() {
		Assets = new OgopogoAssets("biodiversity_ogopogo");
		Config = new OgopogoConfig();
		
		if (Config.DetectionRange >= Config.LoseRange)
		{
			BiodiversityPlugin.Logger.LogInfo("Ogopogo detection range lower than lose range. Disabling Ogopogo spawning until this is fixed.");
		}
		else if (Config.AttackDistance > Config.DetectionRange)
		{
			
			BiodiversityPlugin.Logger.LogInfo("Ogopogo attack distance is not in the detection distance. Disabling Ogopogo spawning until this is fixed.");
		} else {
			Enemies.RegisterEnemy(Assets.OgopogoEnemyType, Enemies.SpawnType.Daytime, Config.OgopogoRarity.VanillaRarities, Config.OgopogoRarity.ModdedRarities, Assets.OgopogoTerminalNode, Assets.OgopogoTerminalKeyword);
		}
		
		if(Config.EnableVermin)
			Enemies.RegisterEnemy(Assets.VerminEnemyType, Enemies.SpawnType.Outside, Config.VerminRarity.VanillaRarities, Config.VerminRarity.ModdedRarities, Assets.VerminTerminalNode, Assets.VerminTerminalKeyword);
	}
}