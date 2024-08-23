using System;
using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

public class CachedDictionary<TKey, TValue>(Func<TKey, TValue> computeValueFunction)
{
    private readonly Dictionary<TKey, NullableObject<TValue>> _cache = new();
    private readonly Func<TKey, TValue> _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));

    public TValue this[TKey key]
    {
        get
        {
            if (_cache.TryGetValue(key, out NullableObject<TValue> cachedValue) && cachedValue.IsNotNull)
                return cachedValue.Value;
            
            cachedValue = new NullableObject<TValue>(_computeValueFunction(key));
            _cache[key] = cachedValue;

            return cachedValue.Value;
        }
    }
    
    public void Reset(TKey key)
    {
        if (_cache.TryGetValue(key, out NullableObject<TValue> value))
            value.Value = default;
    }

    public void ResetAll()
    {
        foreach (TKey key in _cache.Keys)
        {
            _cache[key].Value = default;
        }
    }
}