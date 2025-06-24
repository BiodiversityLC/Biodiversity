using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
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
        
        EnemyAIInstance.Adapter.StopAllPathing();

        EnemyAIInstance.Adapter.Agent.speed = 0;
        EnemyAIInstance.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Blackboard.AgentMaxAcceleration = 50f;

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
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.MovingToStation);
                break;
        }
    }
}