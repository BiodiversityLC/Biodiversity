using Biodiversity.Util;
using Biodiversity.Util.Types;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class AttackingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private bool _isPlayerTargetable;

    protected AttackingPlayerState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
            new TransitionToChasingEscapedPlayer(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = 5f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;

        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0f,
            0.5f);

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);

        _isPlayerTargetable = true;
    }

    internal override void AIIntervalBehaviour()
    {
        if (EnemyAIInstance.PlayerTargetableConditions.IsPlayerTargetable(EnemyAIInstance.ActualTargetPlayer.Value))
        {
            EnemyAIInstance.movingTowardsTargetPlayer = true;
            _isPlayerTargetable = true;
        }
        else _isPlayerTargetable = false;
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        EnemyAIInstance.netcodeController.TargetPlayerClientId.Value =
            EnemyAIInstance.BackupTargetPlayer.actualClientId;
        EnemyAIInstance.BackupTargetPlayer = null;
    }

    private class TransitionToChasingEscapedPlayer(
        AloeServerAI enemyAIInstance,
        AttackingPlayerState attackingPlayerState)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (!(Vector3.Distance(EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    EnemyAIInstance.transform.position) <= 1.5f)) return !attackingPlayerState._isPlayerTargetable;

            EnemyAIInstance.LogVerbose("Player is close to aloe! Killing them!");
            EnemyAIInstance.netcodeController.CrushPlayerClientRpc(
                EnemyAIInstance.BioId, EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);

            return true;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.ChasingEscapedPlayer;
        }
    }
}