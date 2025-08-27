using Biodiversity.Creatures.Core;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAdapter(EnemyAI instance) : IEnemyAdapter
{
    #region Unity Components

    public NavMeshAgent Agent => instance.agent;
    public Animator Animator => instance.creatureAnimator;
    public Transform Transform => instance.transform;
    public Transform EyeTransform => instance.eye;

    #endregion

    public GameObject[] AssignedAINodes
    {
        get => instance.allAINodes;
        set => instance.allAINodes = value;
    }

    public PlayerControllerB TargetPlayer
    {
        get => instance.targetPlayer;
        set => instance.targetPlayer = value;
    }

    public bool IsDead => instance.isEnemyDead;

    public float StunNormalizedTimer => instance.stunNormalizedTimer;

    public float OpenDoorSpeedMultiplier
    {
        get => instance.openDoorSpeedMultiplier;
        set => instance.openDoorSpeedMultiplier = value;
    } //todo: turn this to "open door speed", see DoorLock.OnTriggerStay

    public float AIIntervalLength
    {
        get => instance.AIIntervalTime;
        set => instance.AIIntervalTime = value;
    }

    public int Health
    {
        get => instance.enemyHP;
        set => instance.enemyHP = value;
    }

    public void StopAllPathing()
    {
        // Resets the destination (so imperium doesn't draw the path to some old destination vector we arent using anymore)
        instance.destination =
            RoundManager.Instance.GetNavMeshPosition(instance.transform.position, RoundManager.Instance.navHit, -1f);

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
    /// <returns>Returns true if the <see cref="Agent"/> has reached its destination.</returns>
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

    /// <summary>
    /// Applies the given damage parameter to the health variable.
    /// </summary>
    /// <param name="damage">The damage to apply.</param>
    /// <returns>True if the applied damage results in death.</returns>
    public bool ApplyDamage(int damage)
    { 
        Health -= damage;
        // todo: play damage/hurt sfx
        
        return Health <= 0;
    }
}