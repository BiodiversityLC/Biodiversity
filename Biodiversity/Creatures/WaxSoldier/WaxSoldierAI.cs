using Biodiversity.Creatures.Core.StateMachine;
using GameNetcodeStuff;
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
    
    public WaxSoldierBlackboard Blackboard { get; private set; }
    public WaxSoldierAdapter Adapter { get; private set; }

    #region Event Functions

    public void Awake()
    {
        Blackboard = new WaxSoldierBlackboard();
        Adapter = new WaxSoldierAdapter(this);
    }

    public override void Start()
    {
        base.Start();

        Adapter.Agent.updateRotation = false;
        
        if (!IsServer) return;
        
        InitializeConfigValues();
        
        LogVerbose("Wax Soldier spawned!");
    }
    
    protected override bool ShouldRunUpdate()
    {
        if (!IsServer || Adapter.IsDead)
            return false;

        return true;
    }

    #endregion

    #region Wax Soldier Specific AI Logic

    public void DetermineGuardPostPosition()
    {
        //todo: create tool that lets people easily select good guard spots for the wax soldier (nearly identical to the vending machine placement tool idea)
        
        // for now lets just use this
        Vector3 tempGuardPostPosition = GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position;
        
        Vector3 calculatedPos = tempGuardPostPosition;
        Quaternion calculatedRot = transform.rotation;

        Blackboard.GuardPost = new Pose(calculatedPos, calculatedRot);
    }

    #endregion

    #region Lethal Company Vanilla Events

    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f, 
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer || Adapter.IsDead) return;
        
        CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (!IsServer || Adapter.IsDead) return;
        
        CurrentState?.OnHitEnemy(force, playerWhoHit, hitID);
    }

    #endregion
    
    #region Animation State Callbacks

    public void OnSpawnAnimationStateExit()
    {
        LogVerbose("Spawn animation complete.");
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnSpawnAnimationStateExit));
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
    internal void MoveWithAcceleration()
    {
        float speedAdjustment = Time.deltaTime / 2f;
        Adapter.Agent.speed = Mathf.Lerp(Adapter.Agent.speed, Blackboard.AgentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        Adapter.Agent.acceleration = Mathf.Lerp(Adapter.Agent.acceleration, Blackboard.AgentMaxAcceleration, accelerationAdjustment);
    }
    
    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    internal void InitializeConfigValues()
    {
        if (!IsServer) return;
        
        Adapter.Health = WaxSoldierHandler.Instance.Config.Health;
        Adapter.AIIntervalLength = WaxSoldierHandler.Instance.Config.AiIntervalTime;
        Adapter.OpenDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        Blackboard.ViewWidth = WaxSoldierHandler.Instance.Config.ViewWidth;
        Blackboard.ViewRange = WaxSoldierHandler.Instance.Config.ViewRange;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierAI {BioId}]";
    }

    #endregion
}