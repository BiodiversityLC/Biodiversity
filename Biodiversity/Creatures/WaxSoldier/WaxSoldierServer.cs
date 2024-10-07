using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

internal class WaxSoldierServer : BiodiverseAI
{
#pragma warning disable 0649
    [Header("Controller")] [Space(5f)] [SerializeField] private WaxSoldierClient waxSoldierClient;
#pragma warning restore 0649

    private readonly NullableObject<PlayerControllerB> _actualTargetPlayer = new();
}