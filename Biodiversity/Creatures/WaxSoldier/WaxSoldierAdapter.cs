using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAdapter(EnemyAI instance) : IEnemyAdapter
{
    #region Unity Components

    public NavMeshAgent Agent => instance.agent;

    #endregion
    
    // todo: make sure the (relevant) changes are synced to clients (enemy hp is relevant cuz then clients can see the hp in imperium for example, whereas the door speed multiplier, maybe not)
    
    public bool IsDead => instance.isEnemyDead;

    public float OpenDoorSpeedMultiplier { get => instance.openDoorSpeedMultiplier; set => instance.openDoorSpeedMultiplier = value; } //todo: change this to "open door speed", see DoorLock.OnTriggerStay
    public float AIIntervalLength { get => instance.AIIntervalTime; set => instance.AIIntervalTime = value; }

    public int Health { get => instance.enemyHP; set => instance.enemyHP = value; }

    public void StopAllPathing()
    {
        instance.movingTowardsTargetPlayer = false;
        instance.moveTowardsDestination = false;
    }

    public void SetDestinationToPosition(Vector3 destination)
    {
        instance.SetDestinationToPosition(destination);
    }
}