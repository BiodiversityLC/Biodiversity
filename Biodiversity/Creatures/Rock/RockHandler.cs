using Biodiversity.Util;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;

namespace Biodiversity.Creatures.Rock
{
    internal class RockHandler : BiodiverseAIHandler<RockHandler>
    {
        internal RockAssets Assets { get; private set; }
        public RockHandler()
        {
            Assets = new RockAssets("biodiversity_rock");
            Enemies.RegisterEnemy(Assets.RockEnemyType, 0, Levels.LevelTypes.All, infoNode: null, infoKeyword: null);
        }
    }
}
