using Biodiversity.Core.Attributes;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.Rock;

[HideHandler]
internal class RockHandler : BiodiverseAIHandler<RockHandler>
{
    internal RockAssets Assets { get; private set; }

    public RockHandler()
    {
        // Assets = new RockAssets("biodiversity_rock");
        // Enemies.RegisterEnemy(Assets.RockEnemyType, 0, Levels.LevelTypes.All, infoNode: null, infoKeyword: null);
    }
}