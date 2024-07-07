using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.CustomStateMachineBehaviours;

public class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (NetcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, cannot call RPC to complete spotted animation logic.");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Spotted animation complete.");
        NetcodeController.SpottedAnimationCompleteServerRpc(AloeId);
    }
}