using System.Runtime.CompilerServices;
using UnityEngine;


namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierServerAI : StateManagedAI<WaxSoldierServerAI.States, WaxSoldierServerAI>
{
#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)] 
    public WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649
    
    public enum States
    {
        Spawning,
        Dead,
    }
    
    internal float AgentMaxAcceleration;
    internal float AgentMaxSpeed;
    
    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierServerAI {BioId}]";
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
}