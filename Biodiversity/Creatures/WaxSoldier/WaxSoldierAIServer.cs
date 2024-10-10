using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

internal class WaxSoldierAIServer : StateManagedAI<WaxSoldierAIServer.WaxSoldierStates, WaxSoldierAIServer>
{
#pragma warning disable 0649
    [Header("Controller")] [Space(5f)] 
    [SerializeField] private WaxSoldierClient waxSoldierClient;
#pragma warning restore 0649

    internal enum WaxSoldierStates
    {
        Stationary
    }

    private readonly NullableObject<PlayerControllerB> _actualTargetPlayer = new();
}