using Biodiversity.Util.Animation;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStates;

public class AloeSlapAnimatorState : GenericAnimatorState<AloeServerAI, AloeClient>
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateEnter(animator, stateInfo, layerIndex);
        
        behaviour2.slapCollisionDetection.EnableSlap();
        if (!behaviour1.IsServer) return;
        behaviour1.InSlapAnimation = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        base.OnStateExit(animator, stateInfo, layerIndex);
        
        behaviour2.slapCollisionDetection.DisableSlap();
        if (!behaviour1.IsServer) return;
        behaviour1.InSlapAnimation = false;
        behaviour1.SlappingPlayer.Reset();
    }
}