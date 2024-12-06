using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class CuddlingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    protected CuddlingPlayerState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.agent.speed = 0;
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 4f;

        // EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0.8f, 1f);
        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0.0f,
            0.5f);
    }

    internal override void AIIntervalBehaviour()
    {
        PlayerControllerB tempPlayer = EnemyAIInstance.GetClosestPlayerLookingAtPosition(
            EnemyAIInstance.eye.transform.position,
            ignorePlayer: EnemyAIInstance.ActualTargetPlayer.Value);

        if (tempPlayer != null)
        {
            EnemyAIInstance.netcodeController.LookTargetPosition.Value =
                tempPlayer.gameplayCamera.transform.position;
            EnemyAIInstance.LookAtPosition(tempPlayer.transform.position);
        }
        else if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
        {
            EnemyAIInstance.netcodeController.LookTargetPosition.Value =
                EnemyAIInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
            EnemyAIInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
        }

        EnemyAIInstance.netcodeController.LookTargetPosition.Value =
            EnemyAIInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
    }
}