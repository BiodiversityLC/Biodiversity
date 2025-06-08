using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierServerAI.States.Spawning)]
internal class SpawningState : BehaviourState<WaxSoldierServerAI.States, WaxSoldierServerAI>
{
    public SpawningState(WaxSoldierServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxAcceleration = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.InitializeConfigValues();
        EnemyAIInstance.DeterminePostPosition();
    }
}