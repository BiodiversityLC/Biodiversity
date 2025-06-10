using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Stationary)]
internal class StationaryState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public StationaryState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxAcceleration = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();

        PlayerControllerB currentVisiblePlayer = EnemyAIInstance.GetClosestVisiblePlayerFromEye(
            EnemyAIInstance.eye,
            WaxSoldierHandler.Instance.Config.ViewWidth,
            WaxSoldierHandler.Instance.Config.ViewRange
        );

        if (currentVisiblePlayer == null)
        {
            // figure out what to do
            // possible state change after
        }
    }
}