using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (AloeServerAIInstance.IsServer)
            AloeServerAIInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerAIInstance.aloeId,
                AloeClient.AudioClipTypes.InterruptedHealing);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Spotted animation complete.");
        if (AloeServerAIInstance.IsServer) NetcodeController.HasFinishedSpottedAnimation.Value = true;
    }
}