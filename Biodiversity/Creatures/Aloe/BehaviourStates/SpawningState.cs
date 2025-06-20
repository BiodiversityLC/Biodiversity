using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.Spawning)]
internal class SpawningState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    private bool spawnAnimComplete;
    
    public SpawningState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToRoaming(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        spawnAnimComplete = false;

        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;

        EnemyAIInstance.netcodeController.TargetPlayerClientId.Value = BiodiverseAI.NullPlayerId;
        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.Value = false;

        EnemyAIInstance.InitializeConfigValues();
        EnemyAIInstance.PickFavouriteSpot();
    }
    
    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case nameof(AloeServerAI.OnSpawnAnimationStateExit):
                spawnAnimComplete = true;
                break;
        }
    }
    
    private class TransitionToRoaming(
        AloeServerAI enemyAIInstance,
        SpawningState spawningState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return spawningState.spawnAnimComplete;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.Roaming;
        }
    }
}