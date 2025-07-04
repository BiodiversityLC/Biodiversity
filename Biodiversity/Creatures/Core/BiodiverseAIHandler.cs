using Biodiversity.Patches;
using System;

namespace Biodiversity.Creatures;

internal abstract class BiodiverseAIHandler<T> 
    where T : BiodiverseAIHandler<T> 
{
    internal static T Instance { get; private set; }

    internal BiodiverseAIHandler()
    {
        Instance = (T)this;
    }

    protected void AddSpawnRequirement(EnemyType type, Func<bool> callback)
    {
        RoundManagerPatch.SpawnRequirements.Add(type, callback);
    }
}