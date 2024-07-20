using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class CrushPlayerAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Crush player animation complete.");
        // if (AloeServerInstance.IsServer) 
        
        // This code isnt implemented yet, and im not sure if it will be used
    }
}