using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.WaxSoldier.Misc;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

//todo: write xml doc

// holds variables that the AI needs in all states, and it doesnt include the ones included with the vanilla EnemyAi class (the adapter contains those)
public class WaxSoldierBlackboard : IEnemyBlackboard
{
    public float AgentMaxSpeed { get; set; }
    public float AgentMaxAcceleration { get; set; }
    public float AgentAngularSpeed { get; set; }
    public float ViewWidth { get; set; }
    public float ViewRange { get; set; }
    
    public bool IsNetworkEventsSubscribed { get; set; }
    
    public Pose GuardPost { get; set; }
    
    public Vector3 LastKnownPlayerPosition { get; set; }
    public Vector3 LastKnownPlayerVelocity { get; set; }
    
    public Musket HeldMusket { get; set; }
    public BoxCollider StabAttackTriggerArea { get; set; }
    public AttackSelector AttackSelector {get; set;}
    public AttackAction currentAttackAction { get; set; }
}