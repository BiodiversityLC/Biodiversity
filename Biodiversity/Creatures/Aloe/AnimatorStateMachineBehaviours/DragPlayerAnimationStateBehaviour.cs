using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class DragPlayerAnimationStateBehaviour : AloeStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AloeServerAIInstance.OnDragPlayerAnimationStateEnter();
    }
}