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
		Enemies.RegisterEnemy(Assets.PrototaxEnemyType, Enemies.SpawnType.Daytime, new Dictionary<Levels.LevelTypes, int> { { Levels.LevelTypes.All, 100 } }, [], Assets.PrototaxTerminalNode, Assets.PrototaxTerminalKeyword);
	}
}