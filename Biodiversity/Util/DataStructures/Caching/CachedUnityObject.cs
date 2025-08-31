using System;
using System.Runtime.CompilerServices;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// A struct to cache the lifetime check of a <see cref="UnityEngine.Object"/>.
/// This avoids the per-frame cost of the overloaded '==' operator.
/// </summary>
/// <typeparam name="T">The type of the Unity object. Must inherit from <see cref="UnityEngine.Object"/>.</typeparam>
public struct CachedUnityObject<T> : IEquatable<CachedUnityObject<T>> where T : UnityEngine.Object
{
    private T _value;
    private bool _hasValue;

    /// <summary>
    /// Checks if the cached Unity object is not null and has not been destroyed.
    /// This is a fast, cached boolean read.
    /// </summary>
    public bool HasValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasValue;
    }

    /// <summary>
    /// The underlying Unity object. Always check HasValue before accessing.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// Initializes a new instance and performs the initial lifetime check.
    /// </summary>
    public CachedUnityObject(T value)
    {
        _value = value;
        // The where T : UnityEngine.Object constraint ensures this uses the correct overloaded Unity operator
        _hasValue = value != null;
    }

    /// <summary>
    /// Re-evaluates the value and updates the cached state. Call this if the
    /// object's state might have changed (e.g., it could have been destroyed).
    /// </summary>
    public void Set(T newValue)
    {
        _value = newValue;
        _hasValue = newValue != null;
    }
    
    /// <summary>
    /// Resets the cached object to a null state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _value = null;
        _hasValue = false;
    }

    public override string ToString()
    {
        return _hasValue ? _value.ToString() ?? "null" : "No Value";
    }

    public override bool Equals(object obj)
    {
        return obj is CachedUnityObject<T> other && Equals(other);
    }

    public bool Equals(CachedUnityObject<T> other)
    {
        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return _hasValue ? _value.GetInstanceID() : 0;
    }

    public static bool operator ==(CachedUnityObject<T> left, CachedUnityObject<T> right)
    {
        return left._value == right._value;
    }

    public static bool operator !=(CachedUnityObject<T> left, CachedUnityObject<T> right)
    {
        return left._value != right._value;
    }
}