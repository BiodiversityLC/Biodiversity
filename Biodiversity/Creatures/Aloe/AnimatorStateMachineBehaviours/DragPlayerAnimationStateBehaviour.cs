using Biodiversity.Util;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class DragPlayerAnimationStateBehaviour : AloeStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        BiodiversityPlugin.LogVerbose("Grab player animation complete.");
        if (!AloeServerAIInstance.IsServer) return;

        if (!AloeServerAIInstance.ActualTargetPlayer.HasValue ||
            PlayerUtil.IsPlayerDead(AloeServerAIInstance.ActualTargetPlayer.Value) ||
            !AloeServerAIInstance.ActualTargetPlayer.Value.isInsideFactory)
            AloeServerAIInstance.SwitchBehaviourState(AloeServerAI.AloeStates.Roaming);
        else
            AloeServerAIInstance.GrabTargetPlayer();
    }
}