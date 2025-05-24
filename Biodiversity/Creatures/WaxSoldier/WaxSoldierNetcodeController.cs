using System;
using Unity.Netcode;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierNetcodeController : NetworkBehaviour
{
    internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    
    internal event Action<float, ulong> OnIncreasePlayerFearLevel;
}