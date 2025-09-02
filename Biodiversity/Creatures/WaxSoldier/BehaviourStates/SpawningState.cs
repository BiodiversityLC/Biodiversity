using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using GameNetcodeStuff;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Spawning)]
internal class SpawningState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public SpawningState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.SetMovementProfile(0f, 50f);
        
        EnemyAIInstance.Context.Blackboard.MoltenState = WaxSoldierAI.MoltenState.Unmolten;
        
        // todo: change state initialization to be done in start() instead of awake(), because we should have InitializeConfig() before any state starts doing stuff
        // EnemyAIInstance.Context.Blackboard.NetcodeController.TargetPlayerClientId.SafeSet(BiodiverseAI.NullPlayerId);
        
        // EnemyAIInstance.netcodeController.TargetPlayerClientId.SafeSet(BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.DetermineGuardPostPosition();
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        { 
            case nameof(UnmoltenAnimationHandler.OnSpawnAnimationFinish):
                EnemyAIInstance.StartCoroutine(SpawnMusketWhenNetworkIsReady());
                break;
        }
    }

    private IEnumerator SpawnMusketWhenNetworkIsReady()
    {
        yield return new WaitUntil(() => EnemyAIInstance.Context.Blackboard.IsNetworkEventsSubscribed);
        EnemyAIInstance.Context.Blackboard.NetcodeController.SpawnMusketServerRpc();
        yield return null;
        EnemyAIInstance.PlayAudioClipTypeClientRpc("activateSfx", "creatureVoice", 0, true);
        yield return null;
        EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
    }
    
    internal override bool OnSetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        base.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        return true; // Makes nothing happen
    }

    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        return true; // Makes nothing happen 
    }
}