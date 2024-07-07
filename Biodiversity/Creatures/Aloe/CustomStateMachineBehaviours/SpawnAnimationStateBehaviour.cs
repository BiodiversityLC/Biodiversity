using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.CustomStateMachineBehaviours;

public class SpawnAnimationStateBehaviour : BaseStateMachineBehaviour
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(Spawning, false);

        if (NetcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, cannot call RPC to switch to passive roaming after spawn animation.");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Spawn animation complete, switching to passive roaming.");
        NetcodeController.SpawnAnimationCompleteClientRpc(AloeId);
    }
}