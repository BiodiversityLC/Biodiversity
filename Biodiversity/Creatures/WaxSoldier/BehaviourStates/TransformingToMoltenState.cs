using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using GameNetcodeStuff;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.TransformingToMolten)]
internal class TransformingToMoltenState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public TransformingToMoltenState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        EnemyAIInstance.Context.Blackboard.MoltenState = WaxSoldierAI.MoltenState.Molten;
        
        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.Agent.speed = 0;
        
        EnemyAIInstance.Context.Blackboard.AgentMaxSpeed = 0f;
        EnemyAIInstance.Context.Blackboard.AgentMaxAcceleration = 50f;
    }
    
    internal override bool OnSetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        base.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        return true; // Makes nothing happen
    }

    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        return true; // Makes nothing happen 
    }
}