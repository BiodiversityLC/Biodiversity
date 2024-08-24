using System.Collections.Generic;

namespace Biodiversity.Creatures.Aloe.Types;

/// <summary>
/// Represents an object that can hold a value of type <typeparamref name="T"/> or be null, with an indicator to check its non-null status.
/// </summary>
/// <typeparam name="T">The type of the value being held by this instance.</typeparam>
public class NullableObject<T>
{
    private T _value;
    
    /// <summary>
    /// Gets a value indicating whether the object holds a non-null value.
    /// </summary>
    /// <value>
    /// <c>true</c> if the object holds a non-null value; otherwise, <c>false</c>.
    /// </value>
    public bool IsNotNull { get; private set; }

    /// <summary>
    /// Gets or sets the value of type <typeparamref name="T"/>. 
    /// Setting this property updates the <see cref="IsNotNull"/> property based on whether the value is null or default.
    /// </summary>
    /// <value>
    /// The value of type <typeparamref name="T"/>.
    /// </value>
    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            IsNotNull = !EqualityComparer<T>.Default.Equals(_value, default);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullableObject{T}"/> class with an optional initial value.
    /// </summary>
    /// <param name="value">The initial value of the object, or <c>default(T)</c> if none is provided.</param>
    public NullableObject(T value = default)
    {
        Value = value;
    }
}