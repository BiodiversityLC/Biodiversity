using System;
using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A specialized dictionary that bulk populates key-value pairs on the first access to any key.
/// Once populated, the values are cached and retrieved from the cache on subsequent accesses.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
public class BulkPopulateDictionary<TKey, TValue> : PerKeyCachedDictionary<TKey, TValue>
{
    private readonly Action<Dictionary<TKey, CachedNullable<TValue>>> _populateFunction;
    private bool _isPopulated;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkPopulateDictionary{TKey, TValue}"/> class.
    /// The dictionary will populate all key-value pairs using the provided function the first time any key is accessed.
    /// </summary>
    /// <param name="populateFunction">
    /// A parameterless function that populates the dictionary with key-value pairs. 
    /// This function is called the first time any key is accessed, and after that, 
    /// the dictionary will return cached values for all subsequent accesses.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="populateFunction"/> is <c>null</c>.
    /// </exception>
    public BulkPopulateDictionary(Action<Dictionary<TKey, CachedNullable<TValue>>> populateFunction)
        : base(_ => default!) // We override this, so base function doesn't matter.
    {
        _populateFunction = populateFunction ?? throw new ArgumentNullException(nameof(populateFunction));
        _isPopulated = false;
    }

    /// <summary>
    /// Gets the value associated with the specified key. 
    /// If the dictionary has not been populated yet, it will be populated first by invoking the populate function.
    /// Once populated, values are returned from the cache.
    /// </summary>
    /// <param name="key">The key whose associated value is to be returned.</param>
    /// <returns>
    /// The value associated with the specified key.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if the key is not found after the dictionary is populated.
    /// </exception>
    public override TValue this[TKey key]
    {
        get
        {
            // Populate the dictionary on first access
            if (!_isPopulated)
            {
                _populateFunction(Cache);
                _isPopulated = true;
            }

            // Try to retrieve the value
            if (Cache.TryGetValue(key, out CachedNullable<TValue> cachedValue) && cachedValue.HasValue)
            {
                return cachedValue.Value;
            }

            throw new KeyNotFoundException($"The key '{key}' was not found in the dictionary.");
        }
    }

    /// <summary>
    /// Resets the cached dictionary, causing the populate function to be invoked again 
    /// the next time any key is accessed.
    /// </summary>
    /// <remarks>
    /// This method clears the entire cache and marks the dictionary as unpopulated. 
    /// The next time a key is accessed, the populate function will run again to repopulate the dictionary.
    /// </remarks>
    public void Reset()
    {
        ResetAll();
        _isPopulated = false;
    }
}