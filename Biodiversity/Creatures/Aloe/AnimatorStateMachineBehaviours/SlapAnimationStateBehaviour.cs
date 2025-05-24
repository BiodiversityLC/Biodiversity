using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SlapAnimationStateBehaviour : AloeStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AloeClientInstance.slapCollisionDetection.EnableSlap();
        if (!AloeServerAIInstance.IsServer) return;
        AloeServerAIInstance.InSlapAnimation = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        AloeClientInstance.slapCollisionDetection.DisableSlap();
        if (!AloeServerAIInstance.IsServer) return;
        AloeServerAIInstance.InSlapAnimation = false;
        AloeServerAIInstance.SlappingPlayer.Reset();
    }
}