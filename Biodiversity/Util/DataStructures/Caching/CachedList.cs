using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Provides a thread-safe caching mechanism for storing a list of computed values.
/// The list is computed lazily on first access using a specified function and cached
/// for subsequent accesses until reset. Implements <see cref="IEnumerable{T}"/> to allow iteration.
/// </summary>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
public class CachedList<T> : IEnumerable<T>
{
    private readonly Func<List<T>> _computeListFunction;
    private readonly object _lock = new();

    private List<T> _cachedList;
    private bool _hasValue;

    /// <summary>
    /// Initializes a new thread-safe instance of the <see cref="CachedList{T}"/> class.
    /// </summary>
    /// <param name="computeListFunction">
    /// A function that computes the list of values. This function is invoked only when the list is accessed
    /// for the first time or after the cached list has been reset. The function should ideally return
    /// a non-null list, but null results are handled (treated as computed but empty/invalid).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="computeListFunction"/> is <c>null</c>.
    /// </exception>
    public CachedList(Func<List<T>> computeListFunction)
    {
        _computeListFunction = computeListFunction ?? throw new ArgumentNullException(nameof(computeListFunction));
        _cachedList = null;
        _hasValue = false;
    }

    /// <summary>
    /// Gets the cached list of items. Thread-safe access.
    /// If the list has not been computed yet, it is computed using the provided function
    /// and stored for future access. The computation is guaranteed to happen at most once
    /// between resets, even with concurrent access.
    /// Returns an empty list if the computation function returns null.
    /// </summary>
    /// <value>
    /// The cached list of items of type <typeparamref name="T"/>. Returns an empty list if computation hasn't happened or resulted in null.
    /// </value>
    public List<T> Value
    {
        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField", Justification = "The usage of '_hasValue' outside of the lock is just for performance. The value is checked again inside the lock for correctness.")]
        get
        {
            if (!_hasValue)
            {
                lock (_lock)
                {
                    if (!_hasValue)
                    {
                        ComputeAndCacheListInternal();
                    }
                }
            }

            return _cachedList ?? Enumerable.Empty<T>().ToList();
        }
    }
    
    /// <summary>
    /// Gets a value indicating whether the list has been computed and cached.
    /// Thread-safe access.
    /// </summary>
    public bool HasValue
    {
        get
        {
            lock (_lock)
            {
                return _hasValue;
            }
        }
    }

    /// <summary>
    /// Resets the cached list, causing the next access to <see cref="Value"/> or <see cref="GetEnumerator"/>
    /// to recompute the list using the provided function.
    /// Thread-safe.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _cachedList = null;
            _hasValue = false;
        }
    }
    
    /// <summary>
    /// Internal helper method to compute the list.
    /// MUST be called within a lock.
    /// </summary>
    private void ComputeAndCacheListInternal()
    {
        try
        {
            _cachedList = _computeListFunction();
            _hasValue = true;
        }
        catch
        {
            _hasValue = false;
            throw;
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cached list.
    /// Thread-safe.
    /// If the list has not been computed yet, it will be computed first.
    /// </summary>
    /// <returns>
    /// An enumerator for the cached list of type <typeparamref name="T"/>.
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
        return Value.GetEnumerator();
    }

    /// <summary>
    /// Returns a non-generic enumerator that iterates through the cached list.
    /// </summary>
    /// <returns>
    /// A non-generic <see cref="IEnumerator"/> for the cached list.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}