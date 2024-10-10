using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util.Types;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class DeadState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    protected DeadState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
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
        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(
            EnemyAIInstance.BioId, 0, 0f);

        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.LookTargetPosition, EnemyAIInstance.GetLookAheadVector());
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, AloeServerAI.NullPlayerId);

        AloeSharedData.Instance.Unbind(EnemyAIInstance, BindType.Stalk);

        if (EnemyAIInstance.roamMap.inProgress) EnemyAIInstance.StopSearch(EnemyAIInstance.roamMap);
        EnemyAIInstance.KillEnemyServerRpc(false);
    }
}