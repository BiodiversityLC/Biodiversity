using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Misc;
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
    private float timeSincePlayerLastSeen;
    private const float thresholdTimeWherePlayerGone = 1f;
    
    public PursuitState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        // todo: name config values appropriately
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        
        EnemyAIInstance.Context.Adapter.MoveToPlayer(EnemyAIInstance.Context.Adapter.TargetPlayer);

        timeSincePlayerLastSeen = Time.time;
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
        
        if (EnemyAIInstance.UpdatePlayerLastKnownPosition())
        {
            timeSincePlayerLastSeen = Time.time;

            AttackAction selectedAttack =
                EnemyAIInstance.Context.Blackboard.AttackSelector.SelectAttack(EnemyAIInstance, EnemyAIInstance.Context.Adapter.TargetPlayer);
        
            if (selectedAttack != null)
            {
                StateData data = new();
                data.Add("attackAction", selectedAttack);
                EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Attacking, initData: data);
            }
        }
        else if (Time.time - timeSincePlayerLastSeen >= thresholdTimeWherePlayerGone)
        {
            EnemyAIInstance.SwitchBehaviourState(WaxSoldierAI.States.Hunting);
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