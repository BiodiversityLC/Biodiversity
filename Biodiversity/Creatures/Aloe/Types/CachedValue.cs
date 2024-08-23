using System;

namespace Biodiversity.Creatures.Aloe.Types;

public class CachedValue<T>(Func<T> computeValueFunction)
{
    private readonly NullableObject<T> _cachedValue = new();
    private readonly Func<T> _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));

    public T Value
    {
        get
        {
            if (!_cachedValue.IsNotNull)
                _cachedValue.Value = _computeValueFunction();

            return _cachedValue.Value;
        }
    }

    public void Reset()
    {
        _cachedValue.Value = default;
    }
}