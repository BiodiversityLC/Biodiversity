using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (AloeServerAIInstance.IsServer)
            AloeServerAIInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerAIInstance.BioId,
                AloeClient.AudioClipTypes.InterruptedHealing);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Spotted animation complete.");
        if (AloeServerAIInstance.IsServer) NetcodeController.HasFinishedSpottedAnimation.Value = true;
    }
}