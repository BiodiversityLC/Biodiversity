namespace Biodiversity.Util.DataStructures
{
    // Represents the digging state of an item
    public enum DiggingState
    {
        NotBuried,
        IsBuried,
        Digging,
        FinishDigging = NotBuried,
        CancelDigging = IsBuried
    }
}
