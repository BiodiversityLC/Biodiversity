using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using GameNetcodeStuff;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Dead)]
internal class DeadState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public DeadState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.KillAllSpeed();
        EnemyAIInstance.DropMusket();
        
        EnemyAIInstance.Context.Blackboard.NetcodeController.TargetPlayerClientId.SafeSet(BiodiverseAI.NullPlayerId);
        EnemyAIInstance.Context.Blackboard.NetcodeController.SetAnimationTriggerClientRpc(WaxSoldierClient.Death);
        
        EnemyAIInstance.KillEnemyServerRpc(false);
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