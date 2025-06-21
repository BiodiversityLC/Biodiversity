using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.WalkingToStation)]
internal class WalkingToStationState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    
    public WalkingToStationState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToStationary(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.GuardPost.position);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.MoveWithAcceleration();
        
        NavMeshAgent agent = EnemyAIInstance.agent;

        if (agent.pathPending == false &&
            agent.remainingDistance > 2)
        {
            Vector3 velocity = agent.velocity;
            if (velocity.sqrMagnitude > 0.001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(velocity.normalized);
                EnemyAIInstance.transform.rotation = Quaternion.RotateTowards(EnemyAIInstance.transform.rotation,
                    lookRotation, 100 * Time.deltaTime);
            }
        }
        else if (agent.remainingDistance <= 2)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(EnemyAIInstance.GuardPost.forward);
            EnemyAIInstance.transform.rotation = Quaternion.RotateTowards(EnemyAIInstance.transform.rotation,
                desiredRotation, 100 * Time.deltaTime);
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        //todo: add logic for looking out for players that previously beefed with him
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();

        EnemyAIInstance.transform.position = EnemyAIInstance.GuardPost.position;
        EnemyAIInstance.transform.rotation = Quaternion.LookRotation(EnemyAIInstance.GuardPost.forward);
    }

    private class TransitionToStationary(
        WaxSoldierAI enemyAIInstance)
        : StateTransition<WaxSoldierAI.States, WaxSoldierAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return HasReachedStation();
        }

        internal override WaxSoldierAI.States NextState()
        {
            return WaxSoldierAI.States.Stationary;
        }
        
        private bool HasReachedStation()
        {
            return Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.GuardPost.position) <= 0.1f;
        }
    }
}