using System.Runtime.CompilerServices;
using UnityEngine;


namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierServerAI : StateManagedAI<WaxSoldierServerAI.WaxSoldierStates, WaxSoldierServerAI>
{
    public enum WaxSoldierStates
    {
        Spawning,
        Dead,
    }
    
    internal float AgentMaxAcceleration;
    internal float AgentMaxSpeed;

    protected override WaxSoldierStates DetermineInitialState()
    {
        return WaxSoldierStates.Spawning;
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