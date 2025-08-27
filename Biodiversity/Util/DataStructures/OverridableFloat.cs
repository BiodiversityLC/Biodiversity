using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Biodiversity.Util.DataStructures;

/// <summary>
/// Represents a float value that can be temporarily overridden.
/// Behaves like a value type.
/// </summary>
[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
public struct OverridableFloat : IEquatable<OverridableFloat>
{
    private float _defaultValue;
    private float _overrideValue;
    private bool _isOverridden;
    
    /// <summary>
    /// Initializes a new instance with a default value.
    /// </summary>
    public OverridableFloat(float defaultValue)
    {
        _defaultValue = defaultValue;
        _overrideValue = 0f;
        _isOverridden = false;
    }
    
    /// <summary>Current usable value (override if set, otherwise default).</summary>
    public float Value => _isOverridden ? _overrideValue : _defaultValue;
    
    /// <summary>True if an override is currently active.</summary>
    public readonly bool IsOverridden => _isOverridden;

    /// <summary>Change the default value. Does not affect an active override.</summary>
    /// <param name="value">The new default value.</param>
    public void SetDefaultValue(float value)
    {
        _defaultValue = value;
    }
    
    /// <summary>Temporarily override the value.</summary>
    /// <param name="value">The override value.</param>
    public void Override(float value)
    {
        _overrideValue = value;
        _isOverridden = true;
    }
    
    /// <summary>Clear the override so the value comes from the default again.</summary>
    public void UseDefault()
    {
        _isOverridden = false;
    }
    
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

    public static implicit operator float(OverridableFloat v) => v.Value;
    public static implicit operator OverridableFloat(float v) => new(v);
    
    public bool Equals(OverridableFloat other) => Value == other.Value;
    public override bool Equals(object obj) => obj is OverridableFloat other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(OverridableFloat left, OverridableFloat right) => left.Equals(right);
    public static bool operator !=(OverridableFloat left, OverridableFloat right) => !left.Equals(right);
}