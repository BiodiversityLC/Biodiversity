using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAI : StateManagedAI<WaxSoldierAI.States, WaxSoldierAI>
{
#pragma warning disable 0649
    [SerializeField] private BoxCollider stabAttackTriggerArea;
    [SerializeField] private AttackSelector attackSelector;
    
    [Header("Controllers")] [Space(5f)] 
    public WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649
    
    public enum States
    {
        Spawning,
        MovingToStation,
        ArrivingAtStation,
        Stationary,
        Pursuing,
        Attacking,
        Hunting,
        Dead,
    }

    // public enum OldAttackAction
    // {
    //     None,
    //     Aim,
    //     Fire,
    //     Reload,
    //     Stab,
    //     Spin,
    //     MusketSwing,
    //     CircularFlailing,
    //     Lunge
    // }

    public enum MoltenState
    {
        Unmolten,
        Molten
    }
    /* Molten state ideas:
     *
     * Maybe he can break doors off its hinges like the fiend
     * Sound triangulation
     * Ambush attacks (figure out ambush points by considering where scrap is, apparatus, etc), but don't do cheap annoying stuff like guarding the entrance to the dungeon
     */
    
    public AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> Context { get; private set; }

    #region Event Functions
    public void Awake()
    {
        WaxSoldierBlackboard blackboard = new();
        WaxSoldierAdapter adapter = new(this);

        Context = new AIContext<WaxSoldierBlackboard, WaxSoldierAdapter>(blackboard, adapter);
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        if (!IsServer) return;
        
        SubscribeToNetworkEvents();
        InitializeConfigValues();
        
        LogVerbose("Wax Soldier spawned!");
    }
    #endregion

    #region Wax Soldier Specific AI Logic
    protected override void InitializeGlobalTransitions()
    {
        base.InitializeGlobalTransitions();
        
        GlobalTransitions.Add(new TransitionToDeadState(this));
    }

    public void DetermineGuardPostPosition()
    {
        //todo: create tool that lets people easily select good guard spots for the wax soldier (nearly identical to the vending machine placement tool idea)
        
        // for now lets just use this
        Vector3 tempGuardPostPosition = GetFarthestValidNodeFromPosition(out PathStatus _, agent, transform.position, allAINodes).position;
        
        Vector3 calculatedPos = tempGuardPostPosition;
        Quaternion calculatedRot = transform.rotation;

        Context.Blackboard.GuardPost = new Pose(calculatedPos, calculatedRot);
    }
    
    private void HandleSpawnMusket(NetworkObjectReference objectReference, int scrapValue)
    {
        if (!IsServer) return;
        
        if (!objectReference.TryGet(out NetworkObject networkObject))
        {
            LogError("Received null network object for the musket.");
            return;
        }

        if (!networkObject.TryGetComponent(out Musket receivedMusket))
        {
            LogError("The musket component on the musket network object is null.");
            return;
        }

        LogVerbose("Musket spawned successfully.");
        Context.Blackboard.HeldMusket = receivedMusket;
    }

    public void DropMusket()
    {
        Context.Blackboard.HeldMusket = null;
        netcodeController.DropMusketClientRpc();
    }
    #endregion

    #region Lethal Company Vanilla Events
    public override void SetEnemyStunned(
        bool setToStunned, 
        float setToStunTime = 1f, 
        PlayerControllerB setStunnedByPlayer = null)
    {
        base.SetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        if (!IsServer) return;
        CurrentState?.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        if (!IsServer) return;
        CurrentState?.OnHitEnemy(force, playerWhoHit, hitID);
    }
    #endregion
    
    #region Animation State & Event Calls
    public void OnSpawnAnimationStateExit()
    {
        LogVerbose("Spawn animation complete.");
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnSpawnAnimationStateExit));
    }

    public void OnAttackAnimationFinish()
    {
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnAttackAnimationFinish));
    }
    
    public void OnAnimationEventStabAttackLeap()
    {
        LogVerbose("Stab attack leap.");
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnAnimationEventStabAttackLeap));
    }
    
    public void OnAnimationEventStartTargetLook(string lookTransformName)
    {
        if (!IsServer) return;

        StateData data = new();
        data.Add("lookTransform",
            lookTransformName == "musketMuzzle" ? Context.Blackboard.HeldMusket.muzzleTip : transform);
        
        TriggerCustomEvent(nameof(OnAnimationEventStartTargetLook), data);
    }

    public void OnAnimationEventStopTargetLook()
    {
        if (!IsServer) return;
        LogVerbose("Stop target look.");
        TriggerCustomEvent(nameof(OnAnimationEventStopTargetLook));
    }

    public void OnAnimationEventToggleBayonet()
    {
        if (!IsServer) return;

        MusketBayonetHitbox bayonetHitbox = Context.Blackboard.HeldMusket.bayonetHitbox;
        if (bayonetHitbox.currentBayonetMode == MusketBayonetHitbox.BayonentMode.None)
        {
            int hash = Context.Blackboard.currentAttackAction.AnimationTriggerHash;
            if (hash == WaxSoldierClient.SpinAttack)
            {
                bayonetHitbox.BeginAttack(MusketBayonetHitbox.BayonentMode.Spin);
            }
            if (hash == WaxSoldierClient.StabAttack)
            {
                bayonetHitbox.BeginAttack(MusketBayonetHitbox.BayonentMode.Stab);
            }
            
            LogVerbose($"Toggling bayonet on.");
        }
        else
        {
            LogVerbose($"Toggling bayonet mode off.");
            bayonetHitbox.EndAttack();
        }
    }
    
    public void OnAnimationEventMusketShoot()
    {
        LogVerbose("Musket fired.");
        if (!IsServer) return;
        TriggerCustomEvent(nameof(OnAnimationEventMusketShoot));
    }
    #endregion
    
    #region Little Misc Stuff
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
        Context.Adapter.Agent.speed = Mathf.Lerp(Context.Adapter.Agent.speed, Context.Blackboard.AgentMaxSpeed, speedAdjustment);
        
        float accelerationAdjustment = Time.deltaTime;
        Context.Adapter.Agent.acceleration = Mathf.Lerp(Context.Adapter.Agent.acceleration, Context.Blackboard.AgentMaxAcceleration, accelerationAdjustment);
    }

    internal void DecelerateAndStop()
    {
        // Make the agent's speed smoothly go to zero
        Context.Blackboard.AgentMaxSpeed = 0;
        
        // Boost the acceleration immediately so it can decelerate quickly
        Context.Adapter.Agent.acceleration *= 3;
    }
    
    /// <summary>
    /// Gets the config values and assigns them to their respective [SerializeField] variables.
    /// The variables are [SerializeField] so they can be edited and viewed in the unity inspector, and with the unity explorer in the game
    /// </summary>
    private void InitializeConfigValues()
    {
        if (!IsServer) return;
        LogVerbose("Initializing config values...");
        
        Context.Adapter.Health = WaxSoldierHandler.Instance.Config.Health;
        Context.Adapter.AIIntervalLength = WaxSoldierHandler.Instance.Config.AiIntervalTime;
        Context.Adapter.OpenDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        Context.Blackboard.ViewWidth = WaxSoldierHandler.Instance.Config.ViewWidth;
        Context.Blackboard.ViewRange = WaxSoldierHandler.Instance.Config.ViewRange;
        
        Context.Blackboard.StabAttackTriggerArea = stabAttackTriggerArea;
        Context.Blackboard.AttackSelector = attackSelector;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[WaxSoldierAI {BioId}]";
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (!IsServer || Context.Blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Subscribing to network events.");
        
        netcodeController.OnSpawnMusket += HandleSpawnMusket;
        
        Context.Blackboard.IsNetworkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!IsServer || !Context.Blackboard.IsNetworkEventsSubscribed) return;
        LogVerbose("Unsubscribing from network events.");
        
        netcodeController.OnSpawnMusket -= HandleSpawnMusket;
        
        Context.Blackboard.IsNetworkEventsSubscribed = false;
    }
    #endregion
}