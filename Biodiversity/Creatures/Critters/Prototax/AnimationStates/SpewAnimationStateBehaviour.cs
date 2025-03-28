using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax.AnimationStates;

public class SpewAnimationStateBehaviour : PrototaxBaseAnimationStateBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!PrototaxAiInstance.IsServer) return;
        PrototaxAiInstance.SpewAnimationComplete();
    }
}