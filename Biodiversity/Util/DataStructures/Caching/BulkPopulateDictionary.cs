using System;
using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A thread-safe dictionary that is populated in its entirety on the first access.
/// It uses a factory function to generate the complete set of key-value pairs at once.
/// </summary>
public class BulkPopulateDictionary<TKey, TValue>
{
    private Lazy<Dictionary<TKey, TValue>> _lazyCache;
    private readonly Func<Dictionary<TKey, TValue>> _populateFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkPopulateDictionary{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="populateFunction">
    /// A parameterless function that populates the dictionary with key-value pairs.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="populateFunction"/> is <c>null</c>.
    /// </exception>
    public BulkPopulateDictionary(Func<Dictionary<TKey, TValue>> populateFunction)
    {
        _populateFunction = populateFunction ?? throw new ArgumentNullException(nameof(populateFunction));
        _lazyCache = new Lazy<Dictionary<TKey, TValue>>(_populateFunction);
    }

    /// <summary>
    /// Gets the fully populated dictionary. On first access, this will trigger the value factory
    /// to generate the entire cache. Subsequent accesses return the cached instance.
    /// This property provides full access to all dictionary methods (e.g., foreach, .Keys, .Count).
    /// </summary>
    public Dictionary<TKey, TValue> Value => _lazyCache.Value;

    /// <summary>
    /// Invalidates the entire cache. The next time the dictionary is accessed,
    /// the value factory will be run again to repopulate it from scratch.
    /// </summary>
    public void Invalidate()
    {
        _lazyCache = new Lazy<Dictionary<TKey, TValue>>(_populateFunction);
    }
}