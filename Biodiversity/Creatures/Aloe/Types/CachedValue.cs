using System;

namespace Biodiversity.Creatures.Aloe.Types;

/// <summary>
/// Provides a mechanism to cache the result of a function and retrieve it efficiently on subsequent accesses.
/// </summary>
/// <typeparam name="T">The type of the value to be cached.</typeparam>
public class CachedValue<T>
{
    private readonly NullableObject<T> _cachedValue = new();
    private readonly Func<T> _computeValueFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedValue{T}"/> class with the specified function to compute the value.
    /// </summary>
    /// <param name="computeValueFunction">A function that computes the value to be cached. This function is invoked only when the value is first accessed or when it is reset.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="computeValueFunction"/> is <c>null</c>.</exception>
    public CachedValue(Func<T> computeValueFunction)
    {
        _computeValueFunction = computeValueFunction ?? throw new ArgumentNullException(nameof(computeValueFunction));
    }

    /// <summary>
    /// Gets the cached value. If the value has not been computed yet, the <paramref name="computeValueFunction"/> is invoked to compute and cache the value.
    /// </summary>
    /// <value>The cached value of type <typeparamref name="T"/>.</value>
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
    /// Resets the cached value, causing the next access to <see cref="Value"/> recompute the value using the provided function.
    /// </summary>
    public void Reset()
    {
        _cachedValue.Value = default;
    }
}