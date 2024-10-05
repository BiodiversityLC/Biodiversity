using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Util.Types;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class AttackingPlayerState : BehaviourState
{
    private bool _isPlayerTargetable;

    public AttackingPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance,
        stateType)
    {
        Transitions =
        [
            new TransitionToChasingEscapedPlayer(aloeServerInstance, this)
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        AloeServerInstance.agentMaxSpeed = 5f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.openDoorSpeedMultiplier = 2f;

        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0f,
            0.5f);

        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, false);

        _isPlayerTargetable = true;
    }

    public override void AIIntervalBehaviour()
    {
        if (AloeUtils.IsPlayerTargetable(AloeServerInstance.ActualTargetPlayer.Value))
        {
            AloeServerInstance.movingTowardsTargetPlayer = true;
            _isPlayerTargetable = true;
        }
        else _isPlayerTargetable = false;
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        AloeServerInstance.netcodeController.TargetPlayerClientId.Value =
            AloeServerInstance.backupTargetPlayer.actualClientId;
        AloeServerInstance.backupTargetPlayer = null;
    }

    private class TransitionToChasingEscapedPlayer(
        AloeServer enemyAIInstance,
        AttackingPlayerState attackingPlayerState)
        : StateTransition(enemyAIInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            if (!(Vector3.Distance(EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    EnemyAIInstance.transform.position) <= 1.5f)) return !attackingPlayerState._isPlayerTargetable;

            EnemyAIInstance.LogDebug("Player is close to aloe! Killing them!");
            EnemyAIInstance.netcodeController.CrushPlayerClientRpc(
                EnemyAIInstance.aloeId, EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);

            return true;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.ChasingEscapedPlayer;
        }
    }
}