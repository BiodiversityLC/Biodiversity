using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Spawning)]
internal class SpawningState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private bool spawnAnimComplete;
    
    public SpawningState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToNextState(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        spawnAnimComplete = false;

        EnemyAIInstance.agent.speed = 0;
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.InitializeConfigValues();
        EnemyAIInstance.DetermineGuardPostPosition();
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case nameof(WaxSoldierAI.OnSpawnAnimationStateExit):
                spawnAnimComplete = true;
                break;
        }
    }

    private class TransitionToNextState(
        WaxSoldierAI enemyAIInstance,
        SpawningState spawningState)
        : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return spawningState.spawnAnimComplete;
        }

        internal override WaxSoldierAI.States NextState()
        {
            return WaxSoldierAI.States.WalkingToStation;
        }
    }
}