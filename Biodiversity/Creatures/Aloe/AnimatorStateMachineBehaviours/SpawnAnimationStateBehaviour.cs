using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SpawnAnimationStateBehaviour : BaseStateMachineBehaviour
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(Spawning, false);
        
        if (NetcodeController == null)
        {
            Mls.LogError("Netcode Controller is null, cannot use network variable to switch to passive roaming after spawn animation.");
            return;
        }
        
        if (!NetworkManager.Singleton.IsServer || !NetcodeController.IsOwner) return;
        LogDebug("Spawn animation complete.");
        NetcodeController.HasFinishedSpawnAnimation.Value = true;
    }
}