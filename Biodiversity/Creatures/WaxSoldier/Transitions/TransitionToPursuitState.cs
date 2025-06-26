using Biodiversity.Creatures.Core.StateMachine;
using GameNetcodeStuff;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToPursuitState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    internal override bool ShouldTransitionBeTaken()
    {
        PlayerControllerB player = EnemyAIInstance.GetClosestVisiblePlayerFromEye(
            EnemyAIInstance.Context.Adapter.EyeTransform,
            EnemyAIInstance.Context.Blackboard.ViewWidth,
            EnemyAIInstance.Context.Blackboard.ViewRange);

        if (player)
        {
            EnemyAIInstance.Context.Adapter.TargetPlayer = player;
            return true;
        }
        
        return false;
        // todo: in the distant future, change this to use the threat interface possibly
    }

    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Pursuing;
}