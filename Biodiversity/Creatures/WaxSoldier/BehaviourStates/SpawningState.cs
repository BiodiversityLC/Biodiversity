using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Util;
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
        EnemyAIInstance.Context.Blackboard.MoltenState = WaxSoldierAI.MoltenState.Unmolten;
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();

        EnemyAIInstance.Context.Adapter.Agent.speed = 0;
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;

        EnemyAIInstance.netcodeController.TargetPlayerClientId.SafeSet(BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.DetermineGuardPostPosition();
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnSpawnAnimationStateExit):
                EnemyAIInstance.StartCoroutine(SpawnMusketWhenNetworkIsReady());
                break;
        }
    }

    private IEnumerator SpawnMusketWhenNetworkIsReady()
    {
        yield return new WaitUntil(() => EnemyAIInstance.Context.Blackboard.IsNetworkEventsSubscribed);
        EnemyAIInstance.netcodeController.SpawnMusketServerRpc();
        yield return null;
        EnemyAIInstance.PlayAudioClipTypeClientRpc("activateSfx", "creatureVoice", 0, true);
        yield return null;
        EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
    }
}