namespace Biodiversity.Creatures.Aloe.Types;

public abstract class StateTransition(AloeServer enemyAIInstance)
{
    protected readonly AloeServer EnemyAIInstance = enemyAIInstance;

    public abstract bool ShouldTransitionBeTaken();

    public abstract AloeServer.States NextState();

    public virtual void OnTransition()
    {
    }

    public virtual string GetTransitionDescription()
    {
        return "";
    }
}