using Biodiversity.Creatures.Core;
using Biodiversity.Util.DataStructures;
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
    }

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

    #region Agent Stuff
    public float AgentSpeedChangeRate { get; set; } = 10f;

    private float _targetSpeed;

    internal void MoveAgent()
    {
        Agent.speed = Mathf.MoveTowards(Agent.speed, _targetSpeed, AgentSpeedChangeRate * Time.deltaTime);
    }

    /// <summary>
    /// Sets the desired movement profile for the agent.
    /// The agent's speed will smoothly transition to the new target.
    /// </summary>
    /// <param name="maxSpeed">The desired maximum speed.</param>
    /// <param name="acceleration">The acceleration to use to reach that speed.</param>
    internal void SetMovementProfile(float maxSpeed, float acceleration)
    {
        _targetSpeed = maxSpeed;
        Agent.acceleration = acceleration;
    }

    internal void BeginGracefulStop()
    {
        SetMovementProfile(0f, 100f);
    }

    internal void KillAllSpeed()
    {
        SetMovementProfile(0f, 250f);
        Agent.velocity = Vector3.zero;
        _targetSpeed = 0f;
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
    #endregion

    #region Network Stuff
    public float NetworkPositionInterpolationAggressiveness
    {
        get => instance.syncMovementSpeed;
        set => instance.syncMovementSpeed = value;
    }

    public float NetworkPositionUpdateDistanceTheshold
    {
        get => instance.updatePositionThreshold;
        set => instance.updatePositionThreshold = value;
    }

    public void SetNetworkFidelityProfile(NetworkPositionalSyncFidelity profile)
    {
        NetworkPositionInterpolationAggressiveness = profile.InterpolationAggressiveness;
        NetworkPositionUpdateDistanceTheshold = profile.UpdateDistanceThreshold;
    }

    [Tooltip("High-accuracy, high-bandwidth settings for when waxy is actively fighting a player.")]
    public NetworkPositionalSyncFidelity CombatFidelityProfile = new()
    {
        InterpolationAggressiveness = 0.08f, // Fast and responsive for dodging/aiming.
        UpdateDistanceThreshold = 0.15f      // Send updates for even small movements.
    };

    [Tooltip("Low-accuracy, low-bandwidth settings for when waxy is idle or patrolling.")]
    public NetworkPositionalSyncFidelity PatrolFidelityProfile = new()
    {
        InterpolationAggressiveness = 0.25f, // Smooth and fluid to hide infrequent updates.
        UpdateDistanceThreshold = 1.0f       // Only send an update after moving a full meter.
    };
    #endregion
}