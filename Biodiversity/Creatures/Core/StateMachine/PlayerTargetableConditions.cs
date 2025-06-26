using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Biodiversity.Creatures.Core.StateMachine;

public class PlayerTargetableConditions
{
    private readonly List<Func<PlayerControllerB, bool>> _conditions =
    [
        player => !PlayerUtil.IsPlayerDead(player),
    ];

    public void AddCondition(Func<PlayerControllerB, bool> condition)
    {
        _conditions.Add(condition);
    }

    public bool IsPlayerTargetable(PlayerControllerB player)
    {
        return player && _conditions.All(condition => condition(player));
    }

    public bool IsPlayerTargetable(CachedNullable<PlayerControllerB> player)
    {
        return player.HasValue && _conditions.All(condition => condition(player.Value));
    }
}