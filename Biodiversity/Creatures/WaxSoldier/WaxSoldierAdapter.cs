using Biodiversity.Creatures.Core;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAdapter(EnemyAI instance) : IEnemyAdapter
{
    #region Unity Components

    public NavMeshAgent Agent => instance.agent;
    public Transform EyeTransform => instance.transform; //todo: change this to the actual eye transform when models are gucci

    #endregion
    
    // todo: make sure the (relevant) changes are synced to clients (enemy hp is relevant cuz then clients can see the hp in imperium for example, whereas the door speed multiplier, maybe not)
    
    public bool IsDead => instance.isEnemyDead;

    public float OpenDoorSpeedMultiplier { get => instance.openDoorSpeedMultiplier; set => instance.openDoorSpeedMultiplier = value; } //todo: change this to "open door speed", see DoorLock.OnTriggerStay
    public float AIIntervalLength { get => instance.AIIntervalTime; set => instance.AIIntervalTime = value; }

    public int Health { get => instance.enemyHP; set => instance.enemyHP = value; }

    public void StopAllPathing()
    {
        // Resets the destination (so imperium doesn't draw the path to some old destination vector we arent using anymore)
        instance.destination = RoundManager.Instance.GetNavMeshPosition(instance.transform.position, RoundManager.Instance.navHit, -1f);
        
        instance.movingTowardsTargetPlayer = false;
        instance.moveTowardsDestination = false;
    }

    public void MoveToDestination(Vector3 destination)
    {
        instance.SetDestinationToPosition(destination);
    }

    public void MoveToPlayer(PlayerControllerB player)
    {
        instance.SetMovingTowardsTargetPlayer(player);
        instance.moveTowardsDestination = true;
    }

    /// <summary>
    /// <see href="https://discussions.unity.com/t/how-can-i-tell-when-a-navmeshagent-has-reached-its-destination/52403/5"/>
    /// </summary>
    /// <returns>Returns true if the agent has reached its destination.</returns>
    public bool HasReachedDestination()
    {
        // If there is no destination, then just return true
        if (!instance.moveTowardsDestination) return true;

        // If the agent is still calculating a path, then it has not arrived
        if (Agent.pathPending) return false;

        // If the agent is very close to it's destination, AND it doesn't have a path OR its velocity is near zero, then it has arrived
        if (Agent.remainingDistance <= Agent.stoppingDistance &&
            (!Agent.hasPath || Agent.velocity.sqrMagnitude < 0.05f))
            return true;

        return false;
    }
}