using Biodiversity.Util;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class DragPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Grab player animation complete.");
        if (!AloeServerAIInstance.IsServer) return;

        if (!AloeServerAIInstance.ActualTargetPlayer.IsNotNull ||
            PlayerUtil.IsPlayerDead(AloeServerAIInstance.ActualTargetPlayer.Value) ||
            !AloeServerAIInstance.ActualTargetPlayer.Value.isInsideFactory)
            AloeServerAIInstance.SwitchBehaviourState(AloeServerAI.AloeStates.Roaming);
        else
            AloeServerAIInstance.GrabTargetPlayer();
    }
}