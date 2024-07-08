using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.CustomStateMachineBehaviours;

public class DragPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (NetcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, cannot call RPC to complete grab animation logic.");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Grab animation complete.");
        NetcodeController.GrabTargetPlayerServerRpc(AloeId);
    }
}