using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

//todo: write xml doc so I actually remember what this does in a years time

// holds variables that the AI needs in all states, and it doesnt include the ones included with the vanilla EnemyAi class (the adapter contains those)
public class WaxSoldierBlackboard
{
    public float AgentMaxSpeed { get; set; }
    public float AgentMaxAcceleration { get; set; }
    public float ViewWidth { get; set; }
    public float ViewRange { get; set; }
    
    public Pose GuardPost { get; set; }
}