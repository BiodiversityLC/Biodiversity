using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SlapAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Started slap animation.");
        AloeClientInstance.slapCollisionDetection.EnableSlap();
        if (!AloeServerAIInstance.IsServer) return;
        AloeServerAIInstance.inSlapAnimation = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Finished slap animation.");
        AloeClientInstance.slapCollisionDetection.DisableSlap();
        if (!AloeServerAIInstance.IsServer) return;
        AloeServerAIInstance.inSlapAnimation = false;
        AloeServerAIInstance.SlappingPlayer.Value = null;
    }
}