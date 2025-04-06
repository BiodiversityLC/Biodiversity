using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.AloeStates.ChasingEscapedPlayer)]
internal class ChasingEscapedPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    public float WaitBeforeChasingTimer;

    private bool _isPlayerTargetable;

    public ChasingEscapedPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
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

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.ChasingEscapedPlayerMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.ChasingEscapedPlayerMaxAcceleration;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;
        
        EnemyAIInstance.PlayRandomAudioClipTypeServerRpc(
            AloeClient.AudioClipTypes.chaseSfx.ToString(),
            AloeClient.AudioSourceTypes.aloeVoiceSource.ToString(),
            true, true, false, true);
        
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(EnemyAIInstance.BioId, AloeClient.Stand);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);

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
            if (EnemyAIInstance.PlayerTargetableConditions.IsPlayerTargetable(EnemyAIInstance.ActualTargetPlayer.Value))
            {
                EnemyAIInstance.movingTowardsTargetPlayer = true;
            }
            else
            {
                _isPlayerTargetable = false;
            }
            
            if (BiodiverseAI.DoesEyeHaveLineOfSightToPosition(
                    position: EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    eyeTransform: EnemyAIInstance.eye,
                    width: EnemyAIInstance.ViewWidth,
                    range: EnemyAIInstance.ViewRange))
                EnemyAIInstance.LookAtPosition(EnemyAIInstance.ActualTargetPlayer.Value.transform.position);
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