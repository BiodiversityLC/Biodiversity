using Biodiversity.Util.Attributes;
using Biodiversity.Creatures.StateMachine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Critters.Prototax.BehaviourStates;

[Preserve]
[State(PrototaxAI.PrototaxStates.Spawning)]
internal class SpawningState : BehaviourState<PrototaxAI.PrototaxStates, PrototaxAI>
{
    public SpawningState(PrototaxAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
    }
}