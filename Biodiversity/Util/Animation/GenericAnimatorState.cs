using UnityEngine;

namespace Biodiversity.Util.Animation;

// todo: use a design pattern that makes sense in this context, not this
public abstract class GenericAnimatorState<T1> : BaseAnimatorState where T1 : Component
{
    protected T1 behaviour1 { get; private set; }

    protected override void EnsureInitialized(Animator animator)
    {
        if (IsInitialized) return;
        
        behaviour1 = animator.GetComponent<T1>();
        if (!behaviour1) BiodiversityPlugin.Logger.LogError($"'{GetType().Name}' could not find the required component of type '{typeof(T1).Name}' on the Animator's GameObject.");

        IsInitialized = true;
    }
}

public abstract class GenericAnimatorState<T1, T2> : BaseAnimatorState 
    where T1 : Component
    where T2 : Component
{
    protected T1 behaviour1 { get; private set; }
    protected T2 behaviour2 { get; private set; }

    protected override void EnsureInitialized(Animator animator)
    {
        if (IsInitialized) return;
        
        behaviour1 = animator.GetComponent<T1>();
        if (!behaviour1) BiodiversityPlugin.Logger.LogError($"'{GetType().Name}' could not find the required component of type '{typeof(T1).Name}' on the Animator's GameObject.");
        
        behaviour2 = animator.GetComponent<T2>();
        if (!behaviour2) BiodiversityPlugin.Logger.LogError($"'{GetType().Name}' could not find the required component of type '{typeof(T2).Name}' on the Animator's GameObject.");

        IsInitialized = true;
    }
}