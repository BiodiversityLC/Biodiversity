using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
using Biodiversity.Creatures.WaxSoldier.Transitions;
using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Pursuing)]
internal class PursuitState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
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

        // Sort (alive) players by distance to the wax soldier, and cache the distance calculation
        List<(PlayerControllerB player, float distance)> playersWithDistance = StartOfRound.Instance.allPlayerScripts
            .Where(player => !PlayerUtil.IsPlayerDead(player))
            .Select(player => (
                player,
                distance: Vector3.Distance(player.transform.position, EnemyAIInstance.transform.position)
            ))
            .OrderBy(player => player.distance)
            .ToList();

        AttackAction selectedAttack =
            EnemyAIInstance.Context.Blackboard.AttackSelector.SelectAttack(EnemyAIInstance,
                playersWithDistance[0].player, playersWithDistance[0].distance);
        
        if (selectedAttack != null)
        {
            StateData data = new();
            data.Add("attackAction", selectedAttack);
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Attacking, initData: data);
        }
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
    }
    
    // private float spinAttackRange = 3f;
    // private float spinAttackCooldown = 2f;
    //
    // private float stabAttackRange = 8f;
    // private float stabAttackCooldown = 2f;
    //
    // private bool isStabLeapActivated;
    //
    // private Coroutine attackCoroutine;
    // private WaxSoldierAI.OldAttackAction currentOldAttackState = WaxSoldierAI.OldAttackAction.None;

    // internal override void AIIntervalBehaviour()
    // {
    //     base.AIIntervalBehaviour();
    //     
    //     // Sort (alive) players by distance to the wax soldier, and cache the distance calculation
    //     List<(PlayerControllerB player, float distance)> playersWithDistance = StartOfRound.Instance.allPlayerScripts
    //         .Where(player => !PlayerUtil.IsPlayerDead(player))
    //         .Select(player => (
    //             player,
    //             distance: Vector3.Distance(player.transform.position, EnemyAIInstance.transform.position)
    //         ))
    //         .OrderBy(player => player.distance)
    //         .ToList();
    //
    //     for (int i = 0; i < playersWithDistance.Count; i++)
    //     {
    //         (PlayerControllerB player, float distance) = playersWithDistance[i];
    //         
    //         if (distance <= spinAttackRange)
    //         {
    //             AttackSetup(WaxSoldierAI.OldAttackAction.Spin, player);
    //             return;
    //         }
    //     
    //         bool hasLineOfSightToPlayer = BiodiverseAI.DoesEyeHaveLineOfSightToPosition(
    //             distance, player.transform.position, EnemyAIInstance.Context.Adapter.EyeTransform,
    //             EnemyAIInstance.Context.Blackboard.ViewWidth, EnemyAIInstance.Context.Blackboard.ViewRange, 1f);
    //         
    //         if (distance <= stabAttackRange && hasLineOfSightToPlayer)
    //         {
    //             BoxCollider stabArea = EnemyAIInstance.Context.Blackboard.StabAttackTriggerArea;
    //             int hitCount = Physics.OverlapBoxNonAlloc(stabArea.transform.position,
    //                 stabAttackTriggerAreaHalfExtents.Value, colliderBuffer, Quaternion.identity,
    //                 LayerMask.GetMask("Player"));
    //     
    //             EnemyAIInstance.LogVerbose($"Hit {hitCount} players in stab attack trigger area.");
    //             if (hitCount > 0)
    //             {
    //                 for (int j = 0; j < hitCount; j++)
    //                 {
    //                     Collider detectedCollider = colliderBuffer[j];
    //                     
    //                     if (detectedCollider.CompareTag("Player") &&
    //                         detectedCollider.transform.TryGetComponent(out PlayerControllerB detectedPlayer) &&
    //                         !PlayerUtil.IsPlayerDead(detectedPlayer))
    //                     {
    //                         EnemyAIInstance.LogVerbose($"Stabbing player '{detectedPlayer.playerUsername}'.");
    //                         AttackSetup(WaxSoldierAI.OldAttackAction.Stab, detectedPlayer);
    //                         return;
    //                     }
    //                 }
    //             }
    //         }
    //         else if (hasLineOfSightToPlayer)
    //         {
    //             EnemyAIInstance.LogVerbose($"Shooting player '{player.playerUsername}'.");
    //             AttackSetup(WaxSoldierAI.OldAttackAction.Fire, player);
    //         }
    //         
    //     }
    // }
    
    // internal override void OnStateExit()
    // {
    //     base.OnStateExit();
    //
    //     CancelAttackCoroutine();
    //     EnemyAIInstance.Context.Adapter.StopAllPathing();
    // }
    
    // internal override void OnCustomEvent(string eventName, StateData eventData)
    // {
    //     base.OnCustomEvent(eventName, eventData);
    //     switch (eventName)
    //     {
    //         case nameof(WaxSoldierAI.OnSpinAttackAnimationStateExit):
    //         case nameof(WaxSoldierAI.OnStabAttackAnimationStateExit):
    //             CancelAttackCoroutine();
    //             EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
    //             break;
    //         
    //         case nameof(WaxSoldierAI.OnAnimationEventStabAttackLeap):
    //             isStabLeapActivated = true;
    //             break;
    //         
    //         case nameof(WaxSoldierAI.OnAnimationEventMusketShoot):
    //             CancelAttackCoroutine();
    //             EnemyAIInstance.Context.Blackboard.HeldMusket.StartCoroutine(EnemyAIInstance.Context.Blackboard
    //                 .HeldMusket.Shoot());
    //             EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    //             EnemyAIInstance.Context.Blackboard.HeldMusket.Reload();
    //             break;
    //     }
    // }
    //
    // private IEnumerator DoMusketShootAttack()
    // {
    //     EnemyAIInstance.LogVerbose($"In {nameof(DoMusketShootAttack)}");
    //
    //     EnemyAIInstance.Context.Adapter.StopAllPathing();
    //     EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.ShootMusket);
    //
    //     while (true)
    //     {
    //         Vector3 direction = (EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position -
    //                              EnemyAIInstance.Context.Blackboard.HeldMusket.bulletRayOrigin.position).normalized;
    //         direction.y = 0;
    //         EnemyAIInstance.transform.rotation =
    //             Quaternion.Slerp(EnemyAIInstance.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 25f);
    //         yield return null;
    //     }
    // }
    //
    // private IEnumerator DoSpinAttack()
    // {
    //     EnemyAIInstance.LogVerbose($"In {nameof(DoSpinAttack)}");
    //     EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.bayonetMode = MusketBayonetCollisionDetection.BayonentMode.Spin;
    //
    //     EnemyAIInstance.Context.Adapter.Agent.speed /= 3;
    //     EnemyAIInstance.Context.Adapter.Agent.acceleration *= 3;
    //     EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed / 3;
    //     
    //     EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.SpinAttack);
    //     EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = true;
    //     yield break;
    // }
    //
    // private IEnumerator DoStabAttack()
    // {
    //     EnemyAIInstance.LogVerbose($"In {nameof(DoStabAttack)}");
    //     EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.bayonetMode = MusketBayonetCollisionDetection.BayonentMode.Stab;
    //     
    //     EnemyAIInstance.Context.Adapter.Agent.speed /= 1.5f;
    //     EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = true;
    //     EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.StabAttack);
    //     
    //     yield return new WaitUntil(() => isStabLeapActivated);
    //     EnemyAIInstance.LogVerbose($"Stab leap activated.");
    //     EnemyAIInstance.Context.Adapter.StopAllPathing();
    //     
    //     float leapDistance = Mathf.Max(BiodiverseAI.Distance2d(EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetTip.position,
    //         EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position) - 0.5f, 0f);
    //     
    //     if (leapDistance > 0f)
    //     {
    //         Vector3 directionToPlayer = (EnemyAIInstance.Context.Adapter.TargetPlayer.transform.position -
    //                                      EnemyAIInstance.transform.position).normalized;
    //         EnemyAIInstance.Context.Adapter.Agent.Move(directionToPlayer * leapDistance);
    //     }
    //     
    //     EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);
    // }
    //
    // private void AttackSetup(WaxSoldierAI.OldAttackAction oldAttackAction, PlayerControllerB targetPlayer)
    // {
    //     EnemyAIInstance.Context.Adapter.TargetPlayer = targetPlayer;
    //     CancelAttackCoroutine();
    //
    //     currentOldAttackState = oldAttackAction;
    //     switch (currentOldAttackState)
    //     {
    //         case WaxSoldierAI.OldAttackAction.Spin: EnemyAIInstance.StartCoroutine(DoSpinAttack()); break;
    //         case WaxSoldierAI.OldAttackAction.Stab: EnemyAIInstance.StartCoroutine(DoStabAttack()); break;
    //         case WaxSoldierAI.OldAttackAction.Fire: EnemyAIInstance.StartCoroutine(DoMusketShootAttack()); break;
    //         default: EnemyAIInstance.LogError($"Attack action '{oldAttackAction}' is not defined."); break;
    //     }
    // }
    //
    // private void CancelAttackCoroutine()
    // {
    //     if (attackCoroutine != null)
    //     {
    //         EnemyAIInstance.StopCoroutine(attackCoroutine);
    //         attackCoroutine = null;
    //     }
    //     
    //     currentOldAttackState = WaxSoldierAI.OldAttackAction.None;
    //     isStabLeapActivated = false;
    //     EnemyAIInstance.Context.Blackboard.HeldMusket.bayonetCollisionDetection.colliderEnabled = false;
    // }
}