using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierServerAI.States.Stationary)]
internal class StationaryState : BehaviourState<WaxSoldierServerAI.States, WaxSoldierServerAI>
{
    public StationaryState(WaxSoldierServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxAcceleration = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
    }
}