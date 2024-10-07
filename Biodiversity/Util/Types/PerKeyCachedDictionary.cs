using System;
using System.Collections.Generic;

namespace Biodiversity.Util.Types;

/// <summary>
/// Provides a caching mechanism for storing computed values associated with specific keys.
/// Values are computed on-demand using a specified function and cached for subsequent accesses.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
public class PerKeyCachedDictionary<TKey, TValue>
{
    protected readonly Dictionary<TKey, NullableObject<TValue>> Cache = new();
    private readonly Func<TKey, TValue> _computeValueFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerKeyCachedDictionary{TKey,TValue}"/> class with the specified function to compute values.
    /// </summary>
    /// <param name="computeValueFunction">
    /// A function that computes the value associated with a given key. This function is invoked only when the key is accessed 
    /// for the first time or after its cached value has been reset.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="computeValueFunction"/> is <c>null</c>.
    /// </exception>
    public PerKeyCachedDictionary(Func<TKey, TValue> computeValueFunction)
    {
        _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));
    }

    /// <summary>
    /// Gets the value associated with the specified key. 
    /// If the value is not already cached, it is computed using the provided function and stored in the cache.
    /// </summary>
    /// <param name="key">The key whose value is to be retrieved.</param>
    /// <returns>
    /// The value associated with the specified key.
    /// If the value has not been cached yet, it is computed, cached, and then returned.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the key does not exist and cannot be processed by the computation function.
    /// </exception>
    /// <remarks>
    /// The value is computed lazily; that is, it is only computed when accessed for the first time.
    /// After the first access, the computed value is cached and reused for all subsequent accesses, unless reset.
    /// </remarks>
    public virtual TValue this[TKey key]
    {
        get
        {
            if (Cache.TryGetValue(key, out NullableObject<TValue> cachedValue) && cachedValue.IsNotNull)
                return cachedValue.Value;
            
            cachedValue = new NullableObject<TValue>(_computeValueFunction(key));
            Cache[key] = cachedValue;

            return cachedValue.Value;
        }
    }
    
    /// <summary>
    /// Resets the cached value associated with the specified key, causing it to be recomputed the next time it is accessed.
    /// </summary>
    /// <param name="key">The key whose cached value should be reset.</param>
    /// <remarks>
    /// After calling this method, the value associated with the specified key will be cleared from the cache.
    /// When accessed again, the value will be recomputed using the provided computation function.
    /// </remarks>
    public void Reset(TKey key)
    {
        if (Cache.TryGetValue(key, out NullableObject<TValue> value))
            value.Value = default;
    }

    /// <summary>
    /// Resets all cached values in the dictionary, causing each value to be recomputed the next time it is accessed.
    /// </summary>
    /// <remarks>
    /// This method clears the cache for all keys. Any subsequent access to a key will result in the value being recomputed
    /// and cached again.
    /// </remarks>
    public void ResetAll()
    {
        foreach (TKey key in Cache.Keys)
        {
            Cache[key].Value = default;
        }
    }
}