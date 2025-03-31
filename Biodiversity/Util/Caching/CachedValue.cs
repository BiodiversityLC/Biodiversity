using System;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Provides a mechanism to cache the result of a function and retrieve it efficiently on subsequent accesses.
/// The value is only computed once and then stored until it is reset.
/// </summary>
/// <typeparam name="T">The type of the value to be cached.</typeparam>
public class CachedValue<T>
{
    private readonly NullableObject<T> _cachedValue = new();
    private readonly Func<T> _computeValueFunction;

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

        if (eager) _cachedValue.Value = _computeValueFunction();
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
        get
        {
            if (!_cachedValue.IsNotNull)
                _cachedValue.Value = _computeValueFunction();

            return _cachedValue.Value;
        }
    }

    /// <summary>
    /// Resets the cached value, causing the next access to <see cref="Value"/> to recompute the value using the provided function.
    /// </summary>
    /// <remarks>
    /// This method sets the cached value back to its default state. When <see cref="Value"/> is accessed again after calling this method,
    /// the value will be recomputed using the original function.
    /// </remarks>
    public void Reset()
    {
        _cachedValue.Value = default;
    }
}