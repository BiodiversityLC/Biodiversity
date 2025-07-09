using System;
using Unity.Netcode;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierNetcodeController : NetworkBehaviour
{
    internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    
    internal event Action<int> OnSetAnimationTrigger;
    
    /// <summary>
    /// Invokes the set animator trigger event
    /// This uses the trigger function on an animator object
    /// </summary>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    [ClientRpc]
    internal void SetAnimationTriggerClientRpc(int animationId)
    {
        OnSetAnimationTrigger?.Invoke(animationId);
    }
}