using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Core;

/// <summary>
/// An adapter for interacting with the underlying <see cref="EnemyAI"/> class made by Zeekers (for the vanilla game).
/// <seealso href="https://refactoring.guru/design-patterns/adapter"/>
/// </summary>
public interface IEnemyAdapter
{
    bool IsDead { get; }
    
    float OpenDoorSpeedMultiplier { get; set; }
    float AIIntervalLength { get; set; }
    
    int Health { get; set; }

    void StopAllPathing();
    void MoveToDestination(Vector3 destination);
    void MoveToPlayer(PlayerControllerB player);
}