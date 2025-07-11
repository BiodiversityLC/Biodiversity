using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
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
    private float spinAttackCooldown = 2f;
    
    private float stabAttackRange = 8f;
    private float stabAttackCooldown = 2f;
    private const float stabLeapDuration = 0.25f;

    private float attackCooldown;

    private bool isStabLeapActivated;
    
    private readonly Collider[] colliderBuffer = new Collider[3];
    private readonly CachedValue<Vector3> stabAttackTriggerAreaHalfExtents;

    private Coroutine attackCoroutine;
    private WaxSoldierAI.AttackAction currentAttackState = WaxSoldierAI.AttackAction.None;
    
    public PursuitState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = 
        [
            new TransitionToHuntingState(EnemyAIInstance)
        ];

        stabAttackTriggerAreaHalfExtents =
            new CachedValue<Vector3>(() => EnemyAIInstance.Context.Blackboard.StabAttackTriggerArea.size * 0.5f);
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
        
        attackCooldown -= Time.deltaTime;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        if (currentAttackState is not WaxSoldierAI.AttackAction.None || attackCooldown > 0) return;
        
        // IF a player or several are within spin attack distance, THEN do spin attack and RETURN
        
        // IF a player is within stabbing distance, THEN stab them and RETURN
        
        // IF a player is within line of sight, THEN take aim
            // Wait x seconds in the shooting position if the player dashes behind cover. Whilst waiting, keep checking if the player becomes in view again.
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
                AttackSetup(WaxSoldierAI.AttackAction.Spin, player);
                return;
            }

            if (distance <= stabAttackRange && BiodiverseAI.DoesEyeHaveLineOfSightToPosition(
                    distance, player.transform.position, EnemyAIInstance.Context.Adapter.EyeTransform, 
                    EnemyAIInstance.Context.Blackboard.ViewWidth, EnemyAIInstance.Context.Blackboard.ViewRange, 1f))
            {
                BoxCollider stabArea = EnemyAIInstance.Context.Blackboard.StabAttackTriggerArea;
                int hitCount = Physics.OverlapBoxNonAlloc(stabArea.transform.position,
                    stabAttackTriggerAreaHalfExtents.Value, colliderBuffer, Quaternion.identity,
                    LayerMask.GetMask("Player"));

                EnemyAIInstance.LogVerbose($"Hit {hitCount} players in stab attack trigger area.");
                if (hitCount > 0)
                {
                    for (int j = 0; j < hitCount; j++)
                    {
                        Collider detectedCollider = colliderBuffer[j];
                        
                        if (detectedCollider.CompareTag("Player") &&
                            detectedCollider.transform.TryGetComponent(out PlayerControllerB detectedPlayer) &&
                            !PlayerUtil.IsPlayerDead(detectedPlayer))
                        {
                            EnemyAIInstance.LogVerbose($"Stabbing player '{detectedPlayer.playerUsername}'.");
                            AttackSetup(WaxSoldierAI.AttackAction.Stab, detectedPlayer);
                            return;
                        }
                    }
                }
            }
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
                break;
            
            case nameof(WaxSoldierAI.OnAnimationEventStabAttackLeap):
                isStabLeapActivated = true;
                break;
        }
    }

    private IEnumerator DoSpinAttack()
    {
        EnemyAIInstance.LogVerbose($"In {nameof(DoSpinAttack)}");
        attackCooldown = spinAttackCooldown;
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.bayonetMode = MusketBayonetCollisionDetection.BayonentMode.Spin;

        EnemyAIInstance.Context.Adapter.Agent.speed /= 3;
        EnemyAIInstance.Context.Adapter.Agent.acceleration *= 3;
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed / 3;
        
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.SpinAttack);
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = true;
        yield break;
    }
    
    private IEnumerator DoStabAttack()
    {
        EnemyAIInstance.LogVerbose($"In {nameof(DoStabAttack)}");
        attackCooldown = stabAttackCooldown;
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.bayonetMode = MusketBayonetCollisionDetection.BayonentMode.Stab;
        
        EnemyAIInstance.Context.Adapter.Agent.speed /= 1.5f;
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = true;
        EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.StabAttack);
        
        yield return new WaitUntil(() => isStabLeapActivated);
        EnemyAIInstance.LogVerbose($"Stab leap activated.");
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        
        float leapDistance = Mathf.Max(BiodiverseAI.Distance2d(EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetTip.position,
            EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position) - 0.5f, 0f);
        
        if (leapDistance > 0f)
        {
            Vector3 directionToPlayer = (EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position -
                                         EnemyAIInstance.transform.position).normalized;
            EnemyAIInstance.Context.Adapter.Agent.Move(directionToPlayer * leapDistance);
        }
        
        EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    }

    private void AttackSetup(WaxSoldierAI.AttackAction attackAction, PlayerControllerB targetPlayer)
    {
        EnemyAIInstance.Context.Adapter.TargetPlayer = targetPlayer;
        CancelAttackCoroutine();

        currentAttackState = attackAction;
        switch (currentAttackState)
        {
            case WaxSoldierAI.AttackAction.Spin: EnemyAIInstance.StartCoroutine(DoSpinAttack()); break;
            case WaxSoldierAI.AttackAction.Stab: EnemyAIInstance.StartCoroutine(DoStabAttack()); break;
            default: EnemyAIInstance.LogError($"Attack action '{attackAction}' is not defined."); break;
        }
    }

    private void CancelAttackCoroutine()
    {
        if (attackCoroutine != null)
        {
            EnemyAIInstance.StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        
        currentAttackState = WaxSoldierAI.AttackAction.None;
        isStabLeapActivated = false;
        EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = false;
    }
}