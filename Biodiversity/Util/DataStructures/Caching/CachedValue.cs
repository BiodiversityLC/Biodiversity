using System;
using System.Diagnostics.CodeAnalysis;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Provides a thread-safe mechanism to cache the result of a function and retrieve it efficiently.
/// The value is computed lazily on first access (unless eager loading is specified)
/// and stored until reset. Uses locking to ensure thread safety.
/// </summary>
/// <typeparam name="T">The type of the value to be cached.</typeparam>
public class CachedValue<T>
{
    private readonly Func<T> _computeValueFunction;
    private readonly object _lock = new();

    private T _cachedValue;
    private bool _hasValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedValue{T}"/> class with the specified function to compute the value.
    /// </summary>
    /// <param name="computeValueFunction">
    /// A function that computes the value to be cached. This function is invoked only the first time the value is accessed, or when the cached value is reset.
    /// </param>
    /// <param name="eager">If true, the value will be computed at the time of construction (eager loading). Defaults to false for lazy loading.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="computeValueFunction"/> is <c>null</c>.
    /// </exception>
    public CachedValue(Func<T> computeValueFunction, bool eager = false)
    {
        _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));
        _cachedValue = default;
        _hasValue = false;

        if (eager)
            ComputeAndCacheValueInternal();
    }

    /// <summary>
    /// Gets the cached value. If the value has not been computed yet, the cached value is computed using the provided function
    /// and stored for future access.
    /// </summary>
    /// <value>
    /// The cached value of type <typeparamref name="T"/>.
    /// </value>
    /// <remarks>
    /// The function is only invoked the first time this property is accessed, unless the value is reset.
    /// </remarks>
    public T Value
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
                        ComputeAndCacheValueInternal();
                    }
                }
            }

            return _cachedValue;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the value has been computed and cached.
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
    /// Resets the cached value, causing the next access to <see cref="Value"/> to recompute.
    /// Thread-safe.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _cachedValue = default;
            _hasValue = false;
        }
    }

    /// <summary>
    /// Internal helper to perform computation.
    /// MUST be called within a lock.
    /// </summary>
    private void ComputeAndCacheValueInternal()
    {
        try
        {
            _cachedValue = _computeValueFunction();
            _hasValue = true;
        }
        catch
        {
            _hasValue = false;
            throw;
        }
    }
}