using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Pursuing)]
internal class PursuitState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private float spinAttackRange = 3f;
    private float stabAttackRange = 6f;

    private Coroutine attackCoroutine;

    private WaxSoldierAI.CombatAction currentAttackState = WaxSoldierAI.CombatAction.None;
    
    public PursuitState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToHuntingState(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        // todo: name config values appropriately
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        
        EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        EnemyAIInstance.MoveWithAcceleration();
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        if (currentAttackState is not WaxSoldierAI.CombatAction.None) return;
        
        // IF a player or several are within spin attack distance, THEN do spin attack and RETURN
        
        // IF a player is within stabbing distance, THEN stab them and RETURN
        
        // IF a player is within line of sight, THEN take aim
            // Wait x seconds in the shooting position. Whilst waiting, keep checking if the player becomes in view again.
                // IF they come into view again, THEN fire and RETURN
                // ELSE keep on waiting for x seconds to pass
            // If x seconds passes with no shots fired, THEN go to hunting state and RETURN
        
        // make sure target player is being chased
        
        // Sort (alive) players by distance to the wax soldier, and cache the distance calculation
        List<(PlayerControllerB player, float distance)> playersWithDistance = StartOfRound.Instance.allPlayerScripts
            .Where(player => !PlayerUtil.IsPlayerDead(player))
            .Select(player => (
                player,
                distance: Vector3.Distance(player.transform.position, EnemyAIInstance.transform.position)
            ))
            .OrderBy(player => player.distance)
            .ToList();

        for (int i = 0; i < playersWithDistance.Count; i++)
        {
            (PlayerControllerB player, float distance) = playersWithDistance[i];
            
            if (distance <= spinAttackRange)
            {
                AttackSetup(WaxSoldierAI.CombatAction.Spin, player);
                return;
            }

            if (distance <= stabAttackRange && BiodiverseAI.DoesEyeHaveLineOfSightToPosition(
                    distance, player.transform.position, EnemyAIInstance.Context.Adapter.EyeTransform, 
                    EnemyAIInstance.Context.Blackboard.ViewWidth, EnemyAIInstance.Context.Blackboard.ViewRange))
            {
                AttackSetup(WaxSoldierAI.CombatAction.Stab, player);
                return;
            }
        }
    }

    private IEnumerator DoSpinAttack()
    {
        EnemyAIInstance.LogVerbose($"In {nameof(DoSpinAttack)}");

        EnemyAIInstance.Context.Adapter.Agent.speed /= 2;
        EnemyAIInstance.Context.Adapter.Agent.acceleration *= 3;
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed / 3;
        
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.SpinAttack);
        yield break;
    }
    
    private IEnumerator DoStabAttack()
    {
        EnemyAIInstance.LogVerbose($"In {nameof(DoStabAttack)}");
        
        EnemyAIInstance.Context.Adapter.Agent.speed /= 1.5f;
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.StabAttack);
        
        yield break;
    }

    private void AttackSetup(WaxSoldierAI.CombatAction attackAction, PlayerControllerB targetPlayer)
    {
        EnemyAIInstance.Context.Adapter.TargetPlayer = targetPlayer;
        CancelAttackCoroutine();

        currentAttackState = attackAction;
        switch (currentAttackState)
        {
            case WaxSoldierAI.CombatAction.Spin: EnemyAIInstance.StartCoroutine(DoSpinAttack()); break;
            case WaxSoldierAI.CombatAction.Stab: EnemyAIInstance.StartCoroutine(DoStabAttack()); break;
            default: EnemyAIInstance.LogError($"Attack action '{attackAction}' is not defined."); break;
        }
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();

        CancelAttackCoroutine();
        EnemyAIInstance.Context.Adapter.StopAllPathing();
    }
    
    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case nameof(WaxSoldierAI.OnSpinAttackAnimationStateExit):
            case nameof(WaxSoldierAI.OnStabAttackAnimationStateExit):
                CancelAttackCoroutine();
                EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
                currentAttackState = WaxSoldierAI.CombatAction.None;
                break;
        }
    }

    private void CancelAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            EnemyAIInstance.StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
    }
}