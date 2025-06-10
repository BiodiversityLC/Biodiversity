using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.CuddlingPlayer)]
internal class CuddlingPlayerState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    public CuddlingPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.agent.speed = 0;
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        PlayerControllerB tempPlayer = BiodiverseAI.GetClosestPlayerLookingAtPosition(
            EnemyAIInstance.eye.transform.position,
            ignorePlayer: EnemyAIInstance.ActualTargetPlayer.Value);

        if (tempPlayer != null)
        {
            EnemyAIInstance.LookAtPosition(tempPlayer.transform.position);
        }
        else if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
        {
            EnemyAIInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
        }
    }
}