using System;
using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A dictionary that populates its entire cache from a bulk function on the first access.
/// This version is for standard C# types.
/// </summary>
public class BulkPopulateDictionary<TKey, TValue>
{
    private readonly Lazy<Dictionary<TKey, TValue>> _lazyCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkPopulateDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="populateFunction">
    /// A parameterless function that populates the dictionary with key-value pairs. 
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="populateFunction"/> is <c>null</c>.
    /// </exception>
    public BulkPopulateDictionary(Action<Dictionary<TKey, TValue>> populateFunction)
    {
        if (populateFunction == null) throw new ArgumentNullException(nameof(populateFunction));
        
        _lazyCache = new Lazy<Dictionary<TKey, TValue>>(() =>
        {
            Dictionary<TKey, TValue> cache = new();
            populateFunction(cache);
            return cache;
        });
    }
    
    /// <summary>
    /// Gets the dictionary cache, populating it if this is the first access.
    /// </summary>
    private Dictionary<TKey, TValue> Cache => _lazyCache.Value;

    /// <summary>
    /// Gets the value associated with the specified key. 
    /// Triggers the bulk population if this is the first time the dictionary is accessed.
    /// </summary>
    /// <param name="key">The key whose associated value is to be returned.</param>
    /// <returns>
    /// The value associated with the specified key.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown if the key is not found after the dictionary is populated.
    /// </exception>
    public TValue this[TKey key]
    {
        get
        {
            if (Cache.TryGetValue(key, out TValue value))
            {
                return value;
            }

            throw new KeyNotFoundException($"The key '{key}' was not found in the dictionary.");
        }
    }
    
    public bool TryGetValue(TKey key, out TValue value)
    {
        return Cache.TryGetValue(key, out value);
    }

    /// <summary>
    /// Resets the cached dictionary, causing the populate function to be invoked again the next time any key is accessed.
    /// </summary>
    public void Reset()
    {
        throw new NotImplementedException();
    }
}