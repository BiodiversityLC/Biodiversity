using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStates;

public class AloeSpottedAnimatorState : GenericAnimatorState<AloeServerAI>
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);
        
        behaviour1.OnSpottedAnimationStateEnter();
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);
        
        behaviour1.OnSpottedAnimationStateExit();
    }
}