using Biodiversity.Util.Types;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class SpawningState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    protected SpawningState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(enemyAiInstance, stateType)
    {
        Transitions =
        [
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;

        EnemyAIInstance.netcodeController.TargetPlayerClientId.Value = AloeServerAI.NullPlayerId;
        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.Value = false;

        EnemyAIInstance.InitializeConfigValues();
        EnemyAIInstance.PickFavouriteSpot();
    }
}