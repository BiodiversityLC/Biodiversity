using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Biodiversity.Creatures.HoneyFeeder;
[UsedImplicitly]
internal class HoneyFeederHandler : BiodiverseAIHandler<HoneyFeederHandler> {
    internal HoneyFeederAssets Assets { get; private set; }
    internal HoneyFeederConfig Config { get; private set; }

    public HoneyFeederHandler() {
        Assets = new HoneyFeederAssets("honeyfeeder");
        Config = new HoneyFeederConfig(BiodiversityPlugin.Instance.CreateConfig("honeyfeeder"));

        Enemies.RegisterEnemy(Assets.enemyType, Enemies.SpawnType.Daytime, new Dictionary<Levels.LevelTypes, int> { { Levels.LevelTypes.All, Config.Rarity } }, []);

        AddSpawnRequirement(Assets.enemyType, () => {
            return false;
        });
    }
}
