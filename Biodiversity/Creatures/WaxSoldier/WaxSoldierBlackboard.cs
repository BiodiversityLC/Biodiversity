using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Util.DataStructures;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

//todo: write xml doc

// holds variables that the AI needs in all states, and it doesnt include the ones included with the vanilla EnemyAi class (the adapter contains those)
public class WaxSoldierBlackboard : IEnemyBlackboard
{
    public float AgentAngularSpeed { get; set; } = 250f;
    public float ViewWidth { get; set; }
    public float ViewRange { get; set; }

    public float WaxDurability { get; set; } = 1f;
    public float WaxSofteningTemperature { get; } = 40f;
    public float WaxMeltTemperature { get; } = 60f;
    
    public float TimeWhenTargetPlayerLastSeen { get; set; }
    public float PursuitLingerTime { get; set; } = 2f;
    public float HuntingLingerTime { get; set; } = 30f;
    public float TimeSincePlayerLastSeen => Time.time - TimeWhenTargetPlayerLastSeen;
    
    public bool IsNetworkEventsSubscribed { get; set; }
    public bool IsFriendlyFireEnabled { get; set; }
    
    public Pose GuardPost { get; set; }
    
    public Vector3 LastKnownPlayerPosition { get; set; }
    public Vector3 LastKnownPlayerVelocity { get; set; }
    
    public WaxSoldierAI.MoltenState MoltenState { get; set; }
    
    public WaxSoldierNetcodeController NetcodeController { get; set; }
    public Musket HeldMusket { get; set; }
    public BoxCollider StabAttackTriggerArea { get; set; }
    public AttackSelector AttackSelector {get; set;}
    public AttackAction currentAttackAction { get; set; }
}