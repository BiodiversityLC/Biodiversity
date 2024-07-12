using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SpawnAnimationStateBehaviour : BaseStateMachineBehaviour
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetBool(Spawning, false);

        LogDebug("Spawn animation complete.");
        
        if (AloeServer.IsServer) AloeServer.SwitchBehaviourState(AloeServer.States.PassiveRoaming);
    }
}