namespace Biodiversity.Creatures.Aloe.Types;

public class NullableObject<T> where T : class
{
    private T _value;
    public bool IsNotNull { get; private set; }

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            IsNotNull = _value != null;
        }
    }

    public NullableObject(T value = null)
    {
        Value = value;
    }
}