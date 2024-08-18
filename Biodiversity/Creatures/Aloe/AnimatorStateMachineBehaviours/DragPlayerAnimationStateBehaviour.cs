using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class DragPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Grab player animation complete.");
        if (!AloeServerInstance.IsServer) return;
        
        if (!AloeServerInstance.ActualTargetPlayer.IsNotNull || 
            AloeUtils.IsPlayerDead(AloeServerInstance.ActualTargetPlayer.Value) ||
            !AloeServerInstance.ActualTargetPlayer.Value.isInsideFactory) 
            AloeServerInstance.SwitchBehaviourState(AloeServer.States.Roaming);
        else
            AloeServerInstance.GrabTargetPlayer();
    }
}