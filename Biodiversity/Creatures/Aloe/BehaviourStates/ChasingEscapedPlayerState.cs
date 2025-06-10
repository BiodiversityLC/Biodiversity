using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.ChasingEscapedPlayer)]
internal class ChasingEscapedPlayerState : BehaviourState<AloeServerAI.States, AloeServerAI>
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
        EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        
        EnemyAIInstance.PlayRandomAudioClipTypeServerRpc(
            nameof(AloeClient.AudioClipTypes.chaseSfx),
            nameof(AloeClient.AudioSourceTypes.aloeVoiceSource),
            true, true, false, true);
        
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(AloeClient.Stand);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);

        WaitBeforeChasingTimer = EnemyAIInstance.WaitBeforeChasingEscapedPlayerTime;
        _isPlayerTargetable = true;
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        WaitBeforeChasingTimer -= Time.deltaTime;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        _isPlayerTargetable = true;
        if (WaitBeforeChasingTimer <= 0)
        {
            if (EnemyAIInstance.PlayerTargetableConditions.IsPlayerTargetable(EnemyAIInstance.ActualTargetPlayer))
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
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (chasingEscapedPlayerState.WaitBeforeChasingTimer > 0 ||
                Vector3.Distance(EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    EnemyAIInstance.transform.position) > 1.5f) return false;

            EnemyAIInstance.LogVerbose("Player is close to aloe! Kidnapping him now.");
            EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(AloeClient.Grab);
            return true;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.KidnappingPlayer;
        }
    }

    private class TransitionToPassiveRoaming(
        AloeServerAI enemyAIInstance,
        ChasingEscapedPlayerState chasingEscapedPlayerState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return !chasingEscapedPlayerState._isPlayerTargetable;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.Roaming;
        }
    }
}