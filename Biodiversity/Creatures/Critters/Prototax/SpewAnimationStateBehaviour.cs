using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax;

public class SpewAnimationStateBehaviour : StateMachineBehaviour
{
    private PrototaxAI _prototaxAIInstance;
    
    public void Initialize(PrototaxAI receivedPrototaxAI)
    {
        _prototaxAIInstance = receivedPrototaxAI;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!_prototaxAIInstance.IsServer) return;
        _prototaxAIInstance.SpewAnimationComplete();
    }
}