using System;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A specialized lazy-loading dictionary for UnityEngine.Object types.
/// It re-validates the cached object's lifetime on every access to handle destroyed objects.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the Unity object. Must inherit from UnityEngine.Object.</typeparam>
public class LazyUnityObjectDictionary<TKey, TValue> : LazyDictionary<TKey, TValue>
    where TValue : UnityEngine.Object
{
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
    public LazyUnityObjectDictionary(Func<TKey, TValue> computeValueFunction) : base(computeValueFunction) { }

    /// <summary>
    /// Gets the value for the specified key. It first checks the cache.
    /// If a cached value exists, it verifies it hasn't been destroyed by Unity.
    /// If the value is missing or destroyed, the factory (computeValueFunction) is invoked to create a new one.
    /// </summary>
    public override TValue this[TKey key]
    {
        get
        {
            // First, check if we have a cached value AND if it's still valid (not destroyed).
            // The 'value != null' check uses Unity's overloaded operator correctly due to the constraint.
            if (Cache.TryGetValue(key, out TValue value) && value != null)
            {
                return value;
            }
            
            // If not found or destroyed, generate a new one
            TValue newValue = _computeValueFunction(key);
            Cache[key] = newValue;
            
            return newValue;
        }
    }
}