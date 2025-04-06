using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SpawnAnimationStateBehaviour : AloeStateMachineBehaviour
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(Spawning, false);
        AloeServerAIInstance.OnSpawnAnimationStateExit();
    }
}