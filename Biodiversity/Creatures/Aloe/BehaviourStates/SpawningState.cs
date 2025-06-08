using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.Spawning)]
internal class SpawningState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    public SpawningState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;

        EnemyAIInstance.netcodeController.TargetPlayerClientId.Value = BiodiverseAI.NullPlayerId;
        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.Value = false;

        EnemyAIInstance.InitializeConfigValues();
        EnemyAIInstance.PickFavouriteSpot();
    }
}