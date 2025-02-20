using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (AloeServerAIInstance.IsServer)
        {
            AloeServerAIInstance.PlayRandomAudioClipTypeServerRpc(
                AloeClient.AudioClipTypes.interruptedHealingSfx.ToString(),
                "creatureVoice",
                    true, true, false, true);
        }
            
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        BiodiversityPlugin.LogVerbose("Spotted animation complete.");
        if (AloeServerAIInstance.IsServer) NetcodeController.HasFinishedSpottedAnimation.Value = true;
    }
}