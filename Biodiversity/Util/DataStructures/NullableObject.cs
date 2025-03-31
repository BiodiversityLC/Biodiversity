using System.Collections.Generic;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Represents an object that can hold a value of type <typeparamref name="T"/> or be null, with an indicator to check if the value is non-null.
/// </summary>
/// <typeparam name="T">The type of the value being held by this instance.</typeparam>
public class NullableObject<T>
{
    private T _value;
    
    /// <summary>
    /// Gets a value indicating whether the object holds a non-null or non-default value.
    /// </summary>
    /// <value>
    /// <c>true</c> if the object holds a non-null or non-default value; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// The value of <see cref="IsNotNull"/> is determined by comparing the stored value with the default value for type <typeparamref name="T"/>.
    /// </remarks>
    public bool IsNotNull { get; private set; }

    /// <summary>
    /// Gets or sets the value of type <typeparamref name="T"/>.
    /// Setting this property updates the <see cref="IsNotNull"/> property based on whether the value is null or the default value for the type.
    /// </summary>
    /// <value>The value of type <typeparamref name="T"/>.</value>
    /// <remarks>
    /// If the value is set to <c>null</c> (for reference types) or the default value (for value types), <see cref="IsNotNull"/> will be set to <c>false</c>.
    /// Otherwise, it will be set to <c>true</c>.
    /// </remarks>
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
    /// <param name="value">
    /// The initial value of the object, or <c>default(T)</c> if no value is provided.
    /// </param>
    /// <remarks>
    /// The <see cref="IsNotNull"/> property will be initialized based on whether the provided value is the default value for type <typeparamref name="T"/>.
    /// </remarks>
    public NullableObject(T value = default)
    {
        Value = value;
    }
}