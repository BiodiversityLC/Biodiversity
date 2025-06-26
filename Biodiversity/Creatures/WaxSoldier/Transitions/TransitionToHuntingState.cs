using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToHuntingState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        return !EnemyAIInstance.IsAPlayerInLineOfSightToEye(
            EnemyAIInstance.Context.Adapter.EyeTransform,
            EnemyAIInstance.Context.Blackboard.ViewWidth,
            EnemyAIInstance.Context.Blackboard.ViewRange
        );
    }
    
    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Hunting;

    internal override void OnTransition()
    {
        base.OnTransition();

        EnemyAIInstance.Context.Blackboard.LastKnownPlayerPosition =
            EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position;
        EnemyAIInstance.Context.Blackboard.LastKnownPlayerVelocity =
            PlayerUtil.GetVelocityOfPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    }
}