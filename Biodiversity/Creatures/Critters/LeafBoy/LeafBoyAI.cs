namespace Biodiversity.Creatures.Critters.LeafBoy;

public class LeafBoyAI : StateManagedAI<LeafBoyAI.LeafBoyStates, LeafBoyAI>
{
    public enum LeafBoyStates
    {
        Spawning,
    }

    protected override LeafBoyStates DetermineInitialState()
    {
        return LeafBoyStates.Spawning;
    }
    
    protected override string GetLogPrefix()
    {
        return $"[LeafBoyAI {BioId}]";
    }
}