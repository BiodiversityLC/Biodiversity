using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.AnimatorStates;

// You may be asking "why call it WaxSoldierSpawnAnimatorState instead of SpawnAnimatorState?" - 
// it's because other creatures like the Aloe also have a similarly named class, and in Unity it
// doesn't tell you the namespace of whatever class your selecting (I think), so it's annoying when
// I'm trying to assign the correct class in the Animator, and I have two, identically named classes.
public class WaxSoldierSpawnAnimatorState : BaseAnimatorState
{
    private WaxSoldierAI behaviour;
    
    protected override void EnsureInitialized(Animator animator)
    {
        if (IsInitialized) return;
        
        behaviour = animator.GetComponentInParent<WaxSoldierAI>();
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

        animator.SetBool(WaxSoldierClient.Spawning, false);
        behaviour.OnSpawnAnimationStateExit();
    }
}