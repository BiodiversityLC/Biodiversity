using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;

namespace Biodiversity.Creatures.Core.StateMachine;

public class PlayerTargetableConditions
{
    private readonly List<Func<PlayerControllerB, bool>> _conditions = [];

    public void AddCondition(Func<PlayerControllerB, bool> condition)
    {
        _conditions.Add(condition);
    }

    public void RemoveCondition(Func<PlayerControllerB, bool> condition)
    {
        _conditions.Remove(condition);
    }

    public void ClearConditions()
    {
        _conditions.Clear();
    }

    public bool IsPlayerTargetable(PlayerControllerB player)
    {
        if (!player) return false;
        
        for (int i = 0; i < _conditions.Count; i++)
        {
            Func<PlayerControllerB, bool> condition = _conditions[i];
            if (!condition(player)) return false;
        }

        return true;
    }
    
    public bool IsPlayerTargetable(CachedNullable<PlayerControllerB> player)
    {
        if (!player.HasValue) return false;
        
        for (int i = 0; i < _conditions.Count; i++)
        {
            Func<PlayerControllerB, bool> condition = _conditions[i];
            if (!condition(player.Value)) return false;
        }

        return true;
    }
}