using UnityEngine;

namespace Biodiversity.Util.Animation;

/// <summary>
/// An internal abstract base class to share initialization logic without being directly usable.
/// This prevents code duplication in the public-facing generic overloads.
/// </summary>
public abstract class BaseAnimatorState : StateMachineBehaviour
{
    protected bool IsInitialized { get; set; }
    
    protected abstract void EnsureInitialized(Animator animator);
    
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);
        EnsureInitialized(animator);
    }
        
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);
        EnsureInitialized(animator);
    }
}