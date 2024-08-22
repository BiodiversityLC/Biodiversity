using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SlapAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Started slap animation.");
        AloeClientInstance.slapCollisionDetection.EnableSlap();
        if (!AloeServerInstance.IsServer) return;
        AloeServerInstance.inSlapAnimation = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Finished slap animation.");
        AloeClientInstance.slapCollisionDetection.DisableSlap();
        if (!AloeServerInstance.IsServer) return;
        AloeServerInstance.inSlapAnimation = false;
        AloeServerInstance.SlappingPlayer.Value = null;
    }
}