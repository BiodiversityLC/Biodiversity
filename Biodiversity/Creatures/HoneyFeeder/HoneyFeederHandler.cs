using Biodiversity.General;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;

namespace Biodiversity.Creatures.HoneyFeeder;
internal class HoneyFeederHandler : BiodiverseAIHandler<HoneyFeederHandler> {
    internal HoneyFeederAssets Assets { get; private set; }
    internal HoneyFeederConfig Config { get; private set; }

    public HoneyFeederHandler() {
        Assets = new("honeyfeeder");
        Config = new HoneyFeederConfig(BiodiversityPlugin.Instance.Config);

        Enemies.RegisterEnemy(Assets.enemyType, Enemies.SpawnType.Daytime, new Dictionary<Levels.LevelTypes, int> { { Levels.LevelTypes.All, Config.Rarity } }, []);
    }
}
