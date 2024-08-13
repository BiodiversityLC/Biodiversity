using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SlapAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Started slap animation.");
        if (!AloeServerInstance.IsServer) return;
        
        if (AloeServerInstance.SlapCoroutine != null)
            AloeServerInstance.StopCoroutine(AloeServerInstance.SlapCoroutine);

        AloeServerInstance.inSlapAnimation = true;
        AloeServerInstance.SlapCoroutine = AloeServerInstance.StartCoroutine(AloeServerInstance.SlapIfClose());
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Finished slap animation.");
        if (!AloeServerInstance.IsServer) return;

        AloeServerInstance.inSlapAnimation = false;
        if (AloeServerInstance.SlapCoroutine != null)
        {
            AloeServerInstance.StopCoroutine(AloeServerInstance.SlapCoroutine);
            AloeServerInstance.SlapCoroutine = null;
        }
        else
        {
            LogDebug("Tried to stop the slap animation, but it appears there wasn't one even playing.");
        }
            
    }
}