using System;
using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A dictionary that computes and caches values on-demand for standard C# types.
/// Once a value is computed for a key, it is stored and returned for all subsequent requests.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
public class LazyDictionary<TKey, TValue>
{
    protected readonly Dictionary<TKey, TValue> Cache = new();
    protected readonly Func<TKey, TValue> _computeValueFunction;

    /// <summary>
    /// Initializes a new instance of the class with the specified function to compute values.
    /// </summary>
    /// <param name="computeValueFunction">
    /// A function that computes the value associated with a given key. This function is invoked only when the key is accessed 
    /// for the first time or after its cached value has been reset.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="computeValueFunction"/> param is <c>null</c>.
    /// </exception>
    public LazyDictionary(Func<TKey, TValue> computeValueFunction)
    {
        _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));
    }

    /// <summary>
    /// Gets the value for the specified key. If the key is not in the cache,
    /// the value factory is invoked, and the result is cached and returned.
    /// </summary>
    public virtual TValue this[TKey key]
    {
        get
        {
            if (Cache.TryGetValue(key, out TValue value))
            {
                return value;
            }
            
            TValue newValue = _computeValueFunction(key);
            Cache[key] = newValue;

            return newValue;
        }
    }
    
    /// <summary>
    /// Removes the value for a specific key from the cache, forcing it to be recomputed on next access.
    /// </summary>
    /// <param name="key">The key whose cached value should be reset.</param>
    public void Invalidate(TKey key)
    {
        Cache.Remove(key);
    }

    /// <summary>
    /// Clears the entire cache, forcing all values to be recomputed on next access.
    /// </summary>
    public void Clear()
    {
        Cache.Clear();
    }
}