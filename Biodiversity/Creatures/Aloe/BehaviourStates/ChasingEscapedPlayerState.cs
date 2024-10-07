using Biodiversity.Util.Types;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class ChasingEscapedPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    public float WaitBeforeChasingTimer;

    private bool _isPlayerTargetable;

    protected ChasingEscapedPlayerState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
            new TransitionToPassiveRoaming(EnemyAIInstance, this),
            new TransitionToKidnappingPlayer(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.agentMaxSpeed = 6f;
        EnemyAIInstance.agentMaxAcceleration = 12f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;
        EnemyAIInstance.inGrabAnimation = false;

        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0.9f, 0.5f);
        EnemyAIInstance.netcodeController.PlayAudioClipTypeServerRpc(EnemyAIInstance.BioId, AloeClient.AudioClipTypes.Chase, true);
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(EnemyAIInstance.BioId, AloeClient.Stand);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);

        WaitBeforeChasingTimer = EnemyAIInstance.WaitBeforeChasingEscapedPlayerTime;
        _isPlayerTargetable = true;
    }

    internal override void UpdateBehaviour()
    {
        WaitBeforeChasingTimer -= Time.deltaTime;
    }

    internal override void AIIntervalBehaviour()
    {
        _isPlayerTargetable = true;
        if (WaitBeforeChasingTimer <= 0)
        {
            if (!EnemyAIInstance.movingTowardsTargetPlayer)
            {
                EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId,
                    0f, 0.25f);
            }

            if (AloeUtils.IsPlayerTargetable(EnemyAIInstance.ActualTargetPlayer.Value))
            {
                EnemyAIInstance.movingTowardsTargetPlayer = true;
            }
            else
            {
                _isPlayerTargetable = false;
            }
        }

        if (AloeUtils.DoesEyeHaveLineOfSightToPosition(
                pos: EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                eye: EnemyAIInstance.eye,
                width: EnemyAIInstance.ViewWidth,
                range: EnemyAIInstance.ViewRange))
        {
            EnemyAIInstance.netcodeController.LookTargetPosition.Value =
                EnemyAIInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
            if (WaitBeforeChasingTimer <= 0)
                EnemyAIInstance.LookAtPosition(EnemyAIInstance.ActualTargetPlayer.Value.transform.position);
        }
        else
        {
            EnemyAIInstance.netcodeController.LookTargetPosition.Value = EnemyAIInstance.GetLookAheadVector();
        }
    }

    private class TransitionToKidnappingPlayer(
        AloeServerAI enemyAIInstance,
        ChasingEscapedPlayerState chasingEscapedPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (chasingEscapedPlayerState.WaitBeforeChasingTimer > 0 ||
                Vector3.Distance(EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    EnemyAIInstance.transform.position) > 1.5f) return false;

            EnemyAIInstance.LogVerbose("Player is close to aloe! Kidnapping him now.");
            EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(
                EnemyAIInstance.BioId,
                AloeClient.Grab);
            return true;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.KidnappingPlayer;
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServerAI enemyAIInstance,
        ChasingEscapedPlayerState chasingEscapedPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return !chasingEscapedPlayerState._isPlayerTargetable;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.Roaming;
        }
    }
}