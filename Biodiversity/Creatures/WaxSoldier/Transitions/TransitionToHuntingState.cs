using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Transitions;

internal class TransitionToHuntingState(WaxSoldierAI enemyAIInstance)
    : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
{
    private const float thresholdTimeWherePlayerGone = 1f;
    private float totalTimeWherePlayerGone;
    
    internal override bool ShouldTransitionBeTaken()
    {
        bool foundPlayer = EnemyAIInstance.IsAPlayerInLineOfSightToEye(
            EnemyAIInstance.Context.Adapter.EyeTransform,
            EnemyAIInstance.Context.Blackboard.ViewWidth,
            EnemyAIInstance.Context.Blackboard.ViewRange
        );

        if (!foundPlayer) totalTimeWherePlayerGone += EnemyAIInstance.Context.Adapter.AIIntervalLength;
        else totalTimeWherePlayerGone = 0f;

        return totalTimeWherePlayerGone >= thresholdTimeWherePlayerGone;
    }
    
    internal override WaxSoldierAI.States NextState() => WaxSoldierAI.States.Hunting;

    private LineRenderer arrowRenderer;

    internal override void OnTransition()
    {
        base.OnTransition();

        EnemyAIInstance.Context.Blackboard.LastKnownPlayerPosition =
            EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position;
        EnemyAIInstance.Context.Blackboard.LastKnownPlayerVelocity =
            PlayerUtil.GetVelocityOfPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);

        totalTimeWherePlayerGone = 0f;

        // if (!arrowRenderer)
        // {
        //     arrowRenderer = EnemyAIInstance.gameObject.AddComponent<LineRenderer>();
        //     arrowRenderer.positionCount = 2;
        //     arrowRenderer.startWidth = 0.1f;
        //     arrowRenderer.endWidth = 0.1f;
        //     arrowRenderer.material = new Material(Shader.Find("Sprites/Default"));
        //     arrowRenderer.startColor = Color.red;
        //     arrowRenderer.endColor = Color.red;
        // }
        //
        // Vector3 start = EnemyAIInstance.Context.Blackboard.LastKnownPlayerPosition;
        // Vector3 dir = EnemyAIInstance.Context.Blackboard.LastKnownPlayerVelocity.normalized;
        // Vector3 end = start + dir * 3;
        // arrowRenderer.SetPosition(0, start);
        // arrowRenderer.SetPosition(1, end);
    }
}