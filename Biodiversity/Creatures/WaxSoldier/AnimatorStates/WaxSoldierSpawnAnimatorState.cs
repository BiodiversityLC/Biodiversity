using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.AnimatorStates;

public class WaxSoldierSpawnAnimatorState : GenericAnimatorState<WaxSoldierAI>
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);

        animator.SetBool(WaxSoldierClient.Spawning, false);
        behaviour1.OnSpawnAnimationStateExit();
    }
}