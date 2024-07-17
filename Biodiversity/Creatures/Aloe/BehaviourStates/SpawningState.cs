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
    
    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        
        AloeServerInstance.netcodeController.TargetPlayerClientId.Value = 69420;
        AloeServerInstance.netcodeController.ShouldHaveDarkSkin.Value = false;
        
        AloeServerInstance.InitializeConfigValues();
        AloeServerInstance.PickFavouriteSpot();
    }

    // private class TransitionToPassiveRoaming(AloeServerInstance aloeServerInstance) : StateTransition(aloeServerInstance)
    // {
    //     public override bool ShouldTransitionBeTaken()
    //     {
    //         return !AloeServerInstance.netcodeController.AnimationParamSpawning.Value;
    //     }
    //
    //     public override AloeServerInstance.States NextState()
    //     {
    //         return AloeServerInstance.States.Roaming;
    //     }
    // }
}