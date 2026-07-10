using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.WaxSoldier.Misc;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

//todo: write xml doc

// holds variables that the AI needs in all states, and it doesnt include the ones included with the vanilla EnemyAi class (the adapter contains those)
public class WaxSoldierBlackboard : IEnemyBlackboard
{
    public float ViewWidth { get; set; }
    public float ViewRange { get; set; }

    public float WaxDurability { get; set; } = 1f;
    public float WaxSofteningTemperature { get; } = 40f;
    public float WaxMeltTemperature { get; } = 60f;

    public float TimeWhenTargetPlayerLastSeen { get; set; }
    public float PursuitLingerTime { get; set; } = 1f;
    public float HuntingLingerTime { get; set; } = 30f; // This is effectively the amount of time the soldier spends hunting a player before giving up
    public float TimeSincePlayerLastSeen => Time.time - TimeWhenTargetPlayerLastSeen;

    public bool IsNetworkEventsSubscribed { get; set; }
    public bool IsFriendlyFireEnabled { get; set; }

    public Pose GuardPost { get; set; }

    public Vector3 LastKnownPlayerPosition { get; set; }
    public Vector3 LastKnownPlayerVelocity { get; set; }

    public WaxSoldierAI.MoltenState MoltenState { get; set; }

    #region Attack Stuff
    public List<AttackAction> AvailableAttacks { get; set; } = [];
    public Dictionary<AttackAction, float> AttackCooldownEndTimes { get; } = new();
    public AttackAction CurrentAttackAction { get; set; }

    public Musket HeldMusket { get; set; }
    public float TimeWhenMusketLastFired { get; set; }
    #endregion

    public WaxSoldierNetcodeController NetcodeController { get; set; }

    public AISearchRoutine moltenRoamSearchRoutine { get; set; }
}