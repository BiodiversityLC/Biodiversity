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
        EnemyAIInstance.UpdateWaxDurability();
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
    
    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        
        // Apply the damage and do nothing else
        EnemyAIInstance.Context.Adapter.ApplyDamage(force);
        return true;
    }
}