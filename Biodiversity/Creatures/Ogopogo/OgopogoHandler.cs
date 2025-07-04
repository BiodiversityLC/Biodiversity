using Biodiversity.Util;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.Ogopogo;

[UsedImplicitly]
internal class OgopogoHandler : BiodiverseAIHandler<OgopogoHandler> {
	internal OgopogoAssets Assets { get; private set; }

	internal OgopogoConfig Config { get; private set; }
    
	public OgopogoHandler() {
		Assets = new OgopogoAssets("biodiversity_ogopogo");
		Config = new OgopogoConfig(BiodiversityPlugin.Instance.CreateConfig("ogopogo"));
		
		if (Config.DetectionRange >= Config.LoseRange)
		{
			BiodiversityPlugin.Logger.LogInfo("Ogopogo detection range lower than lose range. Disabling Ogopogo spawning until this is fixed.");
		}
		else if (Config.AttackDistance > Config.DetectionRange)
		{
			BiodiversityPlugin.Logger.LogInfo("Ogopogo attack distance is not in the detection distance. Disabling Ogopogo spawning until this is fixed.");
		} 
		else 
		{
			LethalLibUtils.TranslateTerminalNode(Assets.OgopogoTerminalNode);
			LethalLibUtils.RegisterEnemyWithConfig(
				Config.OgopogoEnabled,
				Config.OgopogoRarity,
				Assets.OgopogoEnemyType,
				Assets.OgopogoTerminalNode,
				Assets.OgopogoTerminalKeyword);
			
		}
		
		LethalLibUtils.TranslateTerminalNode(Assets.VerminTerminalNode);
		LethalLibUtils.RegisterEnemyWithConfig(
			Config.EnableVermin,
			Config.VerminRarity,
			Assets.VerminEnemyType,
			Assets.VerminTerminalNode,
			Assets.VerminTerminalKeyword);
	}
}