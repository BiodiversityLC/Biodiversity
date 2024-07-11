using System.Collections.Generic;
using System.Linq;

namespace Biodiversity.Creatures.Aloe.Types;

public abstract class BehaviourState(AloeServer aloeServerInstance)
{
    protected readonly AloeServer AloeServerInstance = aloeServerInstance;
    public List<StateTransition> Transitions = [];

    public virtual void OnStateEnter(){}
    public virtual void UpdateBehaviour(){}
    public virtual void LateUpdateBehaviour(){}
    public virtual void AIIntervalBehaviour(){}
    public virtual void OnStateExit(){}

    public AloeServer.States GetStateType()
    {
        return AloeServerInstance.StateDictionary
            .FirstOrDefault(x => x.Value == this).Key;
    }
}