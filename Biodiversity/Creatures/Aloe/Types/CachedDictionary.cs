using System;
using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

/// <summary>
/// Provides a caching mechanism for storing computed values associated with specific keys.
/// Values are computed on-demand and cached for subsequent accesses.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
public class CachedDictionary<TKey, TValue>
{
    private readonly Dictionary<TKey, NullableObject<TValue>> _cache = new();
    private readonly Func<TKey, TValue> _computeValueFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedDictionary{TKey, TValue}"/> class with the specified function to compute values.
    /// </summary>
    /// <param name="computeValueFunction">A function that computes the value associated with a given key. This function is invoked only when a key is accessed for the first time or after the value has been reset.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="computeValueFunction"/> is <c>null</c>.</exception>
    public CachedDictionary(Func<TKey, TValue> computeValueFunction)
    {
        _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));
    }

    /// <summary>
    /// Gets the value associated with the specified key. 
    /// If the value is not already cached, it is computed using the provided function and stored in the cache.
    /// </summary>
    /// <param name="key">The key whose value is to be retrieved.</param>
    /// <returns>The value associated with the specified key.</returns>
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
    
    /// <summary>
    /// Resets the cached value associated with the specified key, causing it to be recomputed the next time it is accessed.
    /// </summary>
    /// <param name="key">The key whose cached value should be reset.</param>
    public void Reset(TKey key)
    {
        if (_cache.TryGetValue(key, out NullableObject<TValue> value))
            value.Value = default;
    }

    /// <summary>
    /// Resets all cached values in the dictionary, causing each value to be recomputed the next time it is accessed.
    /// </summary>
    public void ResetAll()
    {
        foreach (TKey key in _cache.Keys)
        {
            _cache[key].Value = default;
        }
    }
}