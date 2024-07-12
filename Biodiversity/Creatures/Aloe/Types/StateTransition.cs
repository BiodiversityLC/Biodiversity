namespace Biodiversity.Creatures.Aloe.Types;

public abstract class StateTransition(AloeServer aloeServerInstance)
{
    protected readonly AloeServer AloeServerInstance = aloeServerInstance;
    
    public abstract bool ShouldTransitionBeTaken();
    
    public abstract AloeServer.States NextState();

    public virtual void OnTransition(){}

    public virtual string GetTransitionDescription()
    {
        return "";
    }
}