using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierServerAI.States.Dead)]
internal class DeadState : BehaviourState<WaxSoldierServerAI.States, WaxSoldierServerAI>
{
    public DeadState(WaxSoldierServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.agent.speed *= 0.1f;
        EnemyAIInstance.agent.acceleration = 200f;
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 200f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.moveTowardsDestination = false;
        EnemyAIInstance.openDoorSpeedMultiplier = 0f;
        EnemyAIInstance.isEnemyDead = true;
        
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);
        
        EnemyAIInstance.KillEnemyServerRpc(false);
    }
    
}