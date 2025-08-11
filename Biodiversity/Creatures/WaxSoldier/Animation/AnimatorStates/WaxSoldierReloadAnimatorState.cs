using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.AnimatorStates;

public class WaxSoldierReloadAnimatorState : BaseAnimatorState
{
    private UnmoltenAnimationHandler behaviour;
    
    protected override void EnsureInitialized(Animator animator)
    {
        if (IsInitialized) return;
        
        behaviour = animator.GetComponent<UnmoltenAnimationHandler>();
        if (behaviour)
        {
            IsInitialized = true;
            return;
        }
        
        BiodiversityPlugin.Logger.LogError($"'{GetType().Name}' could not find the required component of type '{nameof(WaxSoldierAI)}' on the Animator's GameObject.");
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);
        behaviour?.OnReloadAnimationFinish();
    }
}