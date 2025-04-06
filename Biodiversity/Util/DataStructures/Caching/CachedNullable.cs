using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Represents a value of type <typeparamref name="T"/> that caches its non-default state.
/// Optimized for scenarios where the validity state is checked frequently but the value is set less often.
/// Use this struct to avoid repeated expensive checks, such as the overloaded '==' operator on UnityEngine.Object.
/// </summary>
/// <remarks>
/// This is a struct to avoid heap allocations.
/// When <typeparamref name="T"/> is a UnityEngine.Object, setting the Value performs the potentially expensive
/// Unity lifetime check once. Subsequent checks read the cached boolean state.
/// For value types, this behaves similarly to Nullable (T?), but caches the check explicitly.
/// </remarks>
/// <typeparam name="T">The type of the value being held.</typeparam>
public struct CachedNullable<T> : IEquatable<CachedNullable<T>>
{
    private T _value;
    private bool _hasValue;

    /// <summary>
    /// Gets a value indicating whether this instance holds a non-default (and for UnityEngine.Object, non-destroyed) value.
    /// </summary>
    /// <value><c>true</c> if this instance has a valid value; otherwise, <c>false</c>.</value>
    public bool HasValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _hasValue;
        private set => _hasValue = value;
    }

    /// <summary>
    /// Gets the underlying value. Accessing this when <see cref="HasValue"/> is false
    /// might return default(T) or null, depending on T. Always check HasValue first.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
        // No setter exposed directly to force update via Set() or constructor, therefore preventing accidental setting without updating HasValue
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedNullable{T}"/> struct
    /// and caches the initial validity state.
    /// </summary>
    /// <param name="value">The initial value.</param>
    internal CachedNullable(T value)
    {
        _value = default;
        _hasValue = false;
        Set(value);
    }

    /// <summary>
    /// Sets the value and updates the cached validity state (<see cref="HasValue"/>).
    /// This is where the potentially expensive check happens once per assignment.
    /// </summary>
    /// <param name="newValue">The new value to assign.</param>
    public void Set(T newValue)
    {
        _value = newValue;
        _hasValue = !EqualityComparer<T>.Default.Equals(_value, default);
    }
    
    /// <summary>
    /// Resets this instance to its default state (no value).
    /// Equivalent to calling Set(default(T)).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _value = default;
        _hasValue = false;
    }
    
    /// <summary>
    /// Gets the value if <see cref="HasValue"/> is true, otherwise returns default(T).
    /// </summary>
    /// <returns>The value if valid, otherwise default(T).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault()
    {
        return _hasValue ? _value : default;
    }

    /// <summary>
    /// Gets the value if <see cref="HasValue"/> is true, otherwise returns the specified default value.
    /// </summary>
    /// <param name="defaultValue">The value to return if <see cref="HasValue"/> is false.</param>
    /// <returns>The value if valid, otherwise <paramref name="defaultValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(T defaultValue)
    {
        return _hasValue ? _value : defaultValue;
    }

    public override string ToString()
    {
        return _hasValue ? _value.ToString() ?? "null" : "No Value";
    }

    public override bool Equals(object obj)
    {
        return obj is CachedNullable<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _hasValue ? EqualityComparer<T>.Default.GetHashCode(_value) : 0;
    }
    
    public static bool operator ==(CachedNullable<T> left, CachedNullable<T> right) => left.Equals(right);
    public static bool operator !=(CachedNullable<T> left, CachedNullable<T> right) => !left.Equals(right);
    
    public static bool operator ==(CachedNullable<T> left, T right)
    {
        if (!left._hasValue) return EqualityComparer<T>.Default.Equals(default, right); // Compare default(T) == right
        return EqualityComparer<T>.Default.Equals(left._value, right);
    }
    public static bool operator !=(CachedNullable<T> left, T right) => !(left == right);
    public static bool operator ==(T left, CachedNullable<T> right) => right == left; // Reuse logic
    public static bool operator !=(T left, CachedNullable<T> right) => !(right == left);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CachedNullable<T> other)
    {
        if (!_hasValue && !other._hasValue) return true;
        if (_hasValue != other._hasValue) return false;
        return EqualityComparer<T>.Default.Equals(_value, other._value);
    }
}