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

        EnemyAIInstance.Blackboard.AgentMaxSpeed = WaxSoldierHandler.Instance.Config.PatrolMaxSpeed;
        EnemyAIInstance.Blackboard.AgentMaxAcceleration = WaxSoldierHandler.Instance.Config.PatrolMaxAcceleration;
        EnemyAIInstance.Adapter.OpenDoorSpeedMultiplier = WaxSoldierHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        EnemyAIInstance.Adapter.SetDestinationToPosition(EnemyAIInstance.Blackboard.GuardPost.position);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        EnemyAIInstance.MoveWithAcceleration();
        
        NavMeshAgent agent = EnemyAIInstance.Adapter.Agent;

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
            Quaternion desiredRotation = Quaternion.LookRotation(EnemyAIInstance.Blackboard.GuardPost.forward);
            EnemyAIInstance.transform.rotation = Quaternion.RotateTowards(EnemyAIInstance.transform.rotation,
                desiredRotation, 100 * Time.deltaTime);
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        //todo: add logic for looking out for players that previously beefed with him
        // do this via a global transition thing tho (as in in every state, we want to scan for players)
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();

        EnemyAIInstance.transform.position = EnemyAIInstance.Blackboard.GuardPost.position;
        EnemyAIInstance.transform.rotation = Quaternion.LookRotation(EnemyAIInstance.Blackboard.GuardPost.forward);
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
            // todo: figure out whether I should change this to the func bongo made for detecting whether a navmeshagent has reached its destination
            return Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.Blackboard.GuardPost.position) <= 0.1f;
        }
    }
}