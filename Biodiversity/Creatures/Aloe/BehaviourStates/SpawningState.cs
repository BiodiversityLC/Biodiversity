using Biodiversity.Creatures.Aloe.Types;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class SpawningState : BehaviourState
{
    public SpawningState(AloeServer aloeServerInstance) : base(aloeServerInstance)
    {
        Transitions = [new TransitionToPassiveRoaming(aloeServerInstance)];
    }
    
    public override void OnStateEnter()
    {
        AloeServerInstance.netcodeController.HasFinishedSpawnAnimation.Value = false;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
    }

    private class TransitionToPassiveRoaming(AloeServer aloeServerInstance) : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return AloeServerInstance.netcodeController.HasFinishedSpawnAnimation.Value;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.PassiveRoaming;
        }
    }
}