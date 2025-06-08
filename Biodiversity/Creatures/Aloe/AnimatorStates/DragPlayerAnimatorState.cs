using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStates;

public class DragPlayerAnimatorState : GenericAnimatorState<AloeServerAI>
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);
        
        behaviour1.OnDragPlayerAnimationStateEnter();
    }
}