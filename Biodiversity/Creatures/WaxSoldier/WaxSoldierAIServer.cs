using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

internal class WaxSoldierAIServer : StateManagedAI<WaxSoldierAIServer.WaxSoldierState, WaxSoldierAIServer>
{
#pragma warning disable 0649
    [Header("Controller")] [Space(5f)] 
    [SerializeField] private WaxSoldierClient waxSoldierClient;
#pragma warning restore 0649

    internal enum WaxSoldierState
    {
        Spawning,
        Stationary
    }

    private readonly NullableObject<PlayerControllerB> _actualTargetPlayer = new();
    
    protected override WaxSoldierState DetermineInitialState()
    {
        return WaxSoldierState.Spawning;
    }
}