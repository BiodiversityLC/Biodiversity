namespace Biodiversity.Items;

internal abstract class BiodiverseItemHandler<T> where T : BiodiverseItemHandler<T>
{
    internal static T Instance { get; private set; }

    internal BiodiverseItemHandler()
    {
        Instance = (T)this;
    }
}