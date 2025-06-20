using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStates;

public class AloeSpawnAnimatorState : GenericAnimatorState<AloeServerAI>
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);

        animator.SetBool(AloeClient.Spawning, false);
        behaviour1.OnSpawnAnimationStateExit();
    }
}