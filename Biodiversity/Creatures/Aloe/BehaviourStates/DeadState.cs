using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.Dead)]
internal class DeadState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    public DeadState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.agent.speed *= 0.1f;
        EnemyAIInstance.agent.acceleration = 200f;
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 200f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.moveTowardsDestination = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 0f;
        EnemyAIInstance.isEnemyDead = true;

        EnemyAIInstance.netcodeController.AnimationParamDead.Value = true;

        EnemyAIInstance.SetTargetPlayerInCaptivity(false);

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);

        AloeSharedData.Instance.Unbind(EnemyAIInstance, BindType.Stalk);

        if (EnemyAIInstance.roamMap.inProgress) EnemyAIInstance.StopSearch(EnemyAIInstance.roamMap);
        EnemyAIInstance.KillEnemyServerRpc(false);
    }
}