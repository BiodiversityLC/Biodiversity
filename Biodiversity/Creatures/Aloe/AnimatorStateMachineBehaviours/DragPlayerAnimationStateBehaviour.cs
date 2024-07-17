using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class DragPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Grab player animation complete.");
        if (AloeServerInstance.IsServer) AloeServerInstance.GrabTargetPlayer();
    }
}