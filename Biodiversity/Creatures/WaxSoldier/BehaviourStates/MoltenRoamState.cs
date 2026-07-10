using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.MoltenRoam)]
internal class MoltenRoamState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public MoltenRoamState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToPursuitState(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.SetMovementProfile(WaxSoldierHandler.Instance.Config.HuntingMaxSpeed, WaxSoldierHandler.Instance.Config.HuntingAcceleration);
        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.CombatFidelityProfile);

        EnemyAIInstance.StartSearch(EnemyAIInstance.Context.Adapter.Transform.position, EnemyAIInstance.Context.Blackboard.moltenRoamSearchRoutine);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.MoveAgent();
    }

    internal override void OnStateExit(StateTransition<WaxSoldierAI.States, WaxSoldierAI> transition)
    {
        base.OnStateExit(transition);
        EnemyAIInstance.StopSearch(EnemyAIInstance.Context.Blackboard.moltenRoamSearchRoutine);
    }
}