using System.Collections.Generic;
using Biodiversity.General;
using LethalLib.Modules;

namespace Biodiversity.Creatures.Critters;

internal class CritterHandler : BiodiverseAIHandler<CritterHandler> {
	internal CritterAssets Assets { get; }
	internal CritterConfig Config { get; }

	public CritterHandler() {
		Assets = new CritterAssets("critters");
		Config = new CritterConfig(BiodiversityPlugin.Instance.CreateConfig("critters"));
		
		TranslateTerminalNode(Assets.PrototaxTerminalNode);
		Enemies.RegisterEnemy(Assets.PrototaxEnemyType, Enemies.SpawnType.Daytime, Config.FungiRarity.VanillaRarities, Config.FungiRarity.ModdedRarities, Assets.PrototaxTerminalNode, Assets.PrototaxTerminalKeyword);
		
		TranslateTerminalNode(Assets.LeafyBoiTerminalNode);
		Enemies.RegisterEnemy(Assets.LeafyBoiEnemyType, Enemies.SpawnType.Outside, Config.LeafBoyRarity.VanillaRarities, Config.LeafBoyRarity.ModdedRarities, Assets.LeafyBoiTerminalNode, Assets.LeafyBoiTerminalKeyword);
	}
}