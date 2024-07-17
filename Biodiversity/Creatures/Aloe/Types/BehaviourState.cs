using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

public abstract class BehaviourState(AloeServer aloeServerInstance, AloeServer.States stateType)
{
    protected readonly AloeServer AloeServerInstance = aloeServerInstance;

    public List<StateTransition> Transitions = [];
    
    public virtual void OnStateEnter(ref StateData initData)
    {
        AloeServerInstance.LogDebug($"OnStateEnter called for {stateType}.");
        initData ??= new StateData();
    }
    
    public virtual void UpdateBehaviour(){}
    
    public virtual void LateUpdateBehaviour(){}
    
    public virtual void AIIntervalBehaviour(){}

    public virtual void OnStateExit()
    {
        AloeServerInstance.LogDebug($"OnStateExit called for {stateType}.");
    }

    public AloeServer.States GetStateType()
    {
        return stateType;
    }
}