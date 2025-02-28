using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.Types;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.AloeStates.AttackingPlayer)]
internal class AttackingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private bool _isPlayerTargetable;

    public AttackingPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToChasingEscapedPlayer(EnemyAIInstance, this)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.AttackingPlayerMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.AttackingPlayerMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;

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