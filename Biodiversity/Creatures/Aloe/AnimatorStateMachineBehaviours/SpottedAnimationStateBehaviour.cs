using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SpottedAnimationStateBehaviour : AloeStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AloeServerAIInstance.OnSpottedAnimationStateEnter();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AloeServerAIInstance.OnSpottedAnimationStateExit();
    }
}