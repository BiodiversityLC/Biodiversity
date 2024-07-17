using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class CrushPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Crush player animation complete.");
        // if (AloeServerInstance.IsServer) AloeServerInstance.GrabTargetPlayer();
    }
}