using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.AttackingPlayer)]
internal class AttackingPlayerState : BehaviourState<AloeServerAI.States, AloeServerAI>
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
        EnemyAIInstance.openDoorSpeedMultiplier = EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.SafeSet(true);
        EnemyAIInstance.netcodeController.AnimationParamCrawling.SafeSet(true);
        EnemyAIInstance.netcodeController.AnimationParamHealing.SafeSet(true);
        
        _isPlayerTargetable = true;
    }
    
    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        if (EnemyAIInstance.PlayerTargetableConditions.IsPlayerTargetable(EnemyAIInstance.ActualTargetPlayer))
        {
            EnemyAIInstance.movingTowardsTargetPlayer = true;
            _isPlayerTargetable = true;
        }
        else _isPlayerTargetable = false;
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        if (EnemyAIInstance.BackupTargetPlayer != null)
        {
            EnemyAIInstance.netcodeController.TargetPlayerClientId.Value = EnemyAIInstance.BackupTargetPlayer.actualClientId;
            EnemyAIInstance.BackupTargetPlayer = null;
        }
    }

    private class TransitionToChasingEscapedPlayer(
        AloeServerAI enemyAIInstance,
        AttackingPlayerState attackingPlayerState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            if (!(Vector3.Distance(EnemyAIInstance.ActualTargetPlayer.Value.transform.position,
                    EnemyAIInstance.transform.position) <= 1.5f)) return !attackingPlayerState._isPlayerTargetable;

            EnemyAIInstance.LogVerbose("Player is close to aloe! Killing them!");
            EnemyAIInstance.netcodeController.CrushPlayerClientRpc(EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);

            return true;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.ChasingEscapedPlayer;
        }
    }
}