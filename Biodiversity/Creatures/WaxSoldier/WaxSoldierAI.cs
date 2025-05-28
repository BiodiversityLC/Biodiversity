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
        WalkingToStation,
        Stationary,
        Pursuing,
        Dead,
    }

    public enum CombatAction
    {
        None,
        Aim,
        Fire,
        Reload,
        Stab,
        Spin,
        MusketSwing,
        CircularFlailing,
        Lunge
    }

    public enum MoltenState
    {
        Unmolten,
        Molten
    }
    
    internal float AgentMaxAcceleration;
    internal float AgentMaxSpeed;
    private float _takeDamageCooldown;
    
    internal Vector3 StationPosition;

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        InitializeConfigValues();
        
        LogVerbose("Wax Soldier spawned!");
    }
    
    protected override States DetermineInitialState()
    {
        return States.Spawning;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierServerAI {BioId}]";
    }
    
    protected override bool ShouldRunUpdate()
    {
        if (!IsServer || isEnemyDead)
            return false;
        
        // todo: instead of copying the same setup as the Aloe, instead see if making a `StunnedState` or `StunState` would be a more clean approach (not clean but, idk, u get me anyway)
        
        _takeDamageCooldown -= Time.deltaTime;

        return true;
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

    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    internal void InitializeConfigValues()
    {
        if (!IsServer) return;
        
        enemyHP = WaxSoldierHandler.Instance.Config.Health;

        AIIntervalTime = WaxSoldierHandler.Instance.Config.AiIntervalTime;
        openDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
    }
}