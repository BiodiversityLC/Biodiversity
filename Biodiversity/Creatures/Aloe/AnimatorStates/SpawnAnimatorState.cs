using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStates;

public class SpawnAnimatorState : GenericAnimatorState<AloeServerAI>
{
    private static readonly int Spawning = Animator.StringToHash("Spawning");

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);

        animator.SetBool(Spawning, false);
        behaviour1.OnSpawnAnimationStateExit();
    }
}