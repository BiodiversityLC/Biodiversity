using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (NetcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, cannot change network variable to complete spotted animation logic.");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Spotted animation complete.");
        NetcodeController.HasFinishedSpottedAnimation.Value = true;
    }
}