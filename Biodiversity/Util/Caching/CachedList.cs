using System;
using System.Collections;
using System.Collections.Generic;

namespace Biodiversity.Creatures.StateMachine;

/// <summary>
/// Provides a caching mechanism for storing a list of computed values.
/// The list is computed on-demand using a specified function and cached for subsequent accesses.
/// Implements <see cref="IEnumerable{T}"/> to allow iteration over the cached list.
/// </summary>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
public struct CachedList<T> : IEnumerable
{
    private NullableObject<List<T>> _cachedList = new();
    private readonly Func<List<T>> _computeListFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedList{T}"/> class with the specified function to compute the list.
    /// </summary>
    /// <param name="computeListFunction">
    /// A function that computes the list of values. This function is invoked only when the list is accessed 
    /// for the first time or after the cached list has been reset.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="computeListFunction"/> is <c>null</c>.
    /// </exception>
    public CachedList(Func<List<T>> computeListFunction)
    {
        _computeListFunction = computeListFunction ?? throw new ArgumentNullException(nameof(computeListFunction));
    }

    /// <summary>
    /// Gets the cached list of items. If the list has not been computed yet, it is computed using the provided function
    /// and stored for future access.
    /// </summary>
    /// <value>
    /// The cached list of items of type <typeparamref name="T"/>.
    /// </value>
    /// <remarks>
    /// The function is only invoked the first time this property is accessed, unless the list is reset.
    /// </remarks>
    public List<T> Value
    {
        get
        {
            if (!_cachedList.IsNotNull)
            {
                _cachedList.Value = _computeListFunction();
            }

            return _cachedList.Value;
        }
    }

    /// <summary>
    /// Resets the cached list, causing the next access to <see cref="Value"/> to recompute the list using the provided function.
    /// </summary>
    /// <remarks>
    /// This method clears the cached list. When <see cref="Value"/> is accessed again after calling this method,
    /// the list will be recomputed using the original function.
    /// </remarks>
    public void Reset()
    {
        _cachedList.Value = default;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the cached list.
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