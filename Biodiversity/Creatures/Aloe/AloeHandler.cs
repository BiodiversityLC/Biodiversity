using Biodiversity.General;
using LethalLib.Modules;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Biodiversity.Creatures.Aloe;
[UsedImplicitly]
internal class AloeHandler : BiodiverseAIHandler<AloeHandler> {
    internal AloeAssets Assets { get; set; }
    internal AloeConfig Config { get; set; }

    public AloeHandler() {
        Assets = new AloeAssets("aloebracken");
        Config = new AloeConfig(BiodiversityPlugin.Instance.CreateConfig("aloe"));

        Enemies.RegisterEnemy(Assets.enemyType, Enemies.SpawnType.Daytime, new Dictionary<Levels.LevelTypes, int> { { Levels.LevelTypes.All, Config.Rarity } }, []);

        AddSpawnRequirement(Assets.enemyType, () => false);
    }
}