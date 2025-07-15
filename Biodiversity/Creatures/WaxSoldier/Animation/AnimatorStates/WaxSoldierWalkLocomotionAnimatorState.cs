using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.AnimatorStates;

public class WaxSoldierWalkLocomotionAnimatorState : BaseAnimatorState
{
    private WaxSoldierClient behaviour;
    
    protected override void EnsureInitialized(Animator animator)
    {
        if (IsInitialized) return;
        
        behaviour = animator.GetComponentInParent<WaxSoldierClient>();
        if (behaviour)
        {
            IsInitialized = true;
            return;
        }
        
        BiodiversityPlugin.Logger.LogError($"'{GetType().Name}' could not find the required component of type '{nameof(WaxSoldierAI)}' on the Animator's GameObject.");
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateUpdate(animator, stateInfo, layerIndex);
        behaviour?.SetWalkLocomotionAnimationParams();
    }
}