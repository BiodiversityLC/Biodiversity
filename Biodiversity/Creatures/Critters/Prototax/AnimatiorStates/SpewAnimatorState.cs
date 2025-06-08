using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax.AnimatiorStates;

public class SpewAnimatorState : GenericAnimatorState<PrototaxAI>
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!behaviour1.IsServer) return;
        behaviour1.SpewAnimationComplete();
    }
}