using Biodiversity.Creatures.Aloe.Types;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class SpawningState : BehaviourState
{
    public SpawningState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions = 
        [
            
        ];
    }
    
    public override void OnStateEnter()
    {
        base.OnStateEnter();
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        
        AloeServerInstance.netcodeController.TargetPlayerClientId.Value = 69420;
        AloeServerInstance.netcodeController.ShouldHaveDarkSkin.Value = false;
        
        AloeServerInstance.InitializeConfigValues();
        AloeServerInstance.PickFavouriteSpot();
    }

    // private class TransitionToPassiveRoaming(AloeServer aloeServerInstance) : StateTransition(aloeServerInstance)
    // {
    //     public override bool ShouldTransitionBeTaken()
    //     {
    //         return !AloeServerInstance.netcodeController.AnimationParamSpawning.Value;
    //     }
    //
    //     public override AloeServer.States NextState()
    //     {
    //         return AloeServer.States.PassiveRoaming;
    //     }
    // }
}