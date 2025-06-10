using Biodiversity.Creatures.Core.StateMachine;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.Prototax;

public class PrototaxAI : StateManagedAI<PrototaxAI.PrototaxStates, PrototaxAI>
{
    public enum PrototaxStates
    {
        Spawning,
        Roaming,
        Idle,
        RunningAway
    }
    
#pragma warning disable CS0649
    
    [Header("Spore")] [Space(5f)]
    [SerializeField] private GameObject sporeCloudObject;
    [SerializeField] private Transform sporeCloudOrigin;
    
    [Header("AI and Pathfinding")] [Space(5f)] 
    [SerializeField] private AISearchRoutine roamSearchRoutine;
#pragma warning restore CS0649
    
    private readonly NetworkVariable<bool> _spewingAnimationParam = new();
    private readonly NetworkVariable<bool> _sporeVisible = new();
    
    internal float AgentMaxAcceleration;
    internal float AgentMaxSpeed;
    private float _takeDamageCooldown;

    internal void SpewAnimationComplete()
    {
        
    }

    protected override bool ShouldRunUpdate()
    {
        if (!IsServer || isEnemyDead)
            return false;
        
        _takeDamageCooldown -= Time.deltaTime;

        CalculateSpeed();

        return true;
    }
    
    protected override bool ShouldRunAiInterval()
    {
        return IsServer && !isEnemyDead;
    }
    
    protected override bool ShouldRunLateUpdate()
    {
        return ShouldRunAiInterval();
    }

    private void CalculateSpeed()
    {
        if (stunNormalizedTimer > 0 ||
            CurrentState.GetStateType() == PrototaxStates.Spawning)
        {
            agent.speed = 0;
            agent.acceleration = AgentMaxAcceleration;
        }
        else
        {
            MoveWithAcceleration();
        }
    }
    
    /// <summary>
    /// Makes the agent move by using <see cref="Mathf.Lerp"/> to make the movement smooth
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        agent.speed = Mathf.Lerp(agent.speed, AgentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        agent.acceleration = Mathf.Lerp(agent.acceleration, AgentMaxAcceleration, accelerationAdjustment);
    }

    protected override PrototaxStates DetermineInitialState()
    {
        return PrototaxStates.Spawning;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[PrototaxAI {BioId}]";
    }
}