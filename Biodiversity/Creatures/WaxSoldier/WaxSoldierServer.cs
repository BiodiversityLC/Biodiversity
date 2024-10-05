using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierServer : BiodiverseAI
{
#pragma warning disable 0649
    [Header("Controller")] [Space(5f)] 
    [SerializeField] private WaxSoldierClient waxSoldierClient;
#pragma warning restore 0649

    public enum States
    {
        
    }

    private BehaviourState _previousState;
    private Dictionary<States, BehaviourState> _stateDictionary = [];
    private BehaviourState _currentState;

    private readonly NullableObject<PlayerControllerB> _actualTargetPlayer = new();
}