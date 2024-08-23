using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

public class NullableObject<T>
{
    private T _value;
    public bool IsNotNull { get; private set; }

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            IsNotNull = !EqualityComparer<T>.Default.Equals(_value, default);
        }
    }

    public NullableObject(T value = default)
    {
        Value = value;
    }
}