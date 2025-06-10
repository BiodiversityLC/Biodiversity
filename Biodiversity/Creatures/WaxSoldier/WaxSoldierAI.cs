using Biodiversity.Creatures.Core.StateMachine;
using GameNetcodeStuff;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAI : StateManagedAI<WaxSoldierAI.States, WaxSoldierAI>
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
    
    internal Vector3 PostPosition;

    #region Event Functions

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        InitializeConfigValues();
        
        LogVerbose("Wax Soldier spawned!");
    }
    
    protected override bool ShouldRunUpdate()
    {
        if (!IsServer || isEnemyDead)
            return false;
        
        _takeDamageCooldown -= Time.deltaTime;
        
        return true;
    }

    #endregion

    #region Wax Soldier Specific AI Logic

    public void DeterminePostPosition()
    {
        //todo: create tool that lets people easily select good guard spots for the wax soldier (nearly identical to the vending machine placement tool idea)
        Vector3 calculatedPos = transform.position;

        PostPosition = calculatedPos;
    }

    #endregion

    #region Lethal Company Vanilla Events

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f, 
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer || isEnemyDead) return;
        
        CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (!IsServer || isEnemyDead) return;
        
        CurrentState?.OnHitEnemy(force, playerWhoHit, hitID);
    }

    #endregion
    
    #region Other

    protected override States DetermineInitialState()
    {
        return States.Spawning;
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
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierServerAI {BioId}]";
    }

    #endregion
}