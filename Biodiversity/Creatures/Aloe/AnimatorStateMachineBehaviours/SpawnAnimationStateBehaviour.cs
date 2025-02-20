using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SpawnAnimationStateBehaviour : BaseStateMachineBehaviour
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(Spawning, false);
        BiodiversityPlugin.LogVerbose("Spawn animation complete.");
        if (AloeServerAIInstance.IsServer) AloeServerAIInstance.SwitchBehaviourState(AloeServerAI.AloeStates.Roaming);
    }
}