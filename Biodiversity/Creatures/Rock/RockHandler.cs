using Biodiversity.Core.Attributes;
using Biodiversity.Util;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.Rock;

[UsedImplicitly]
// [HideHandler]
internal class RockHandler : BiodiverseAIHandler<RockHandler>
{
    internal RockAssets Assets { get; private set; }

    public RockHandler()
    {
        Assets = new RockAssets("biodiversity_rock");
        LethalLibUtils.RegisterEnemyWithConfig(true, "All:0", Assets.RockEnemyType);
    }
}