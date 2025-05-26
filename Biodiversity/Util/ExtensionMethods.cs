using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Util;

/// <summary>
/// Provides extension methods for various types to enhance functionality.
/// </summary>
internal static class ExtensionMethods 
{
    /// <summary>
    /// Calculates the normalized direction vector from a source point to a target point.
    /// </summary>
    /// <param name="from">The source position vector</param>
    /// <param name="to">The target position vector</param>
    /// <returns>Normalized direction vector pointing from source to target</returns>
    /// <remarks>This function was made for the Honey Feeder, and I don't think it's needed anymore</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector3 Direction(this Vector3 from, Vector3 to) 
    {
        return (to - from).normalized;
    }

    /// <summary>
    /// Converts normalized time of day to concrete hours and minutes.
    /// Accounts for potential floating point precision loss by adding 360 minutes offset before conversion.
    /// </summary>
    /// <param name="timeOfDay">TimeOfDay instance being extended</param>
    /// <returns>Tuple containing current hour (24h format) and minutes</returns>
    /// <remarks>
    /// This function was made for the Honey Feeder, and I don't think it's needed anymore
    /// </remarks>
    [SuppressMessage("ReSharper", "PossibleLossOfFraction", Justification = "Disabled message because Rider was complaining about totalMinutes / 60")]
    internal static (int hours, int minutes) GetCurrentTime(this TimeOfDay timeOfDay) 
    {
        int totalMinutes = Mathf.FloorToInt(timeOfDay.normalizedTimeOfDay * 60f * timeOfDay.numberOfHours + 360);
        int hour = Mathf.FloorToInt(totalMinutes / 60);

        return (hour, totalMinutes % 60);
    }
    
    /// <summary>
    /// Parses a time string in "HH:mm" format into numeric values
    /// </summary>
    /// <param name="timeOfDay">TimeOfDay instance being extended</param>
    /// <param name="timeString">Time string to parse (format: "H:mm")</param>
    /// <returns>Tuple containing hours and minutes as integers</returns>
    /// <exception cref="FormatException">Thrown if input string is not in correct format</exception>
    /// <exception cref="OverflowException">Thrown if values exceed integer limits</exception>
    /// <remarks>This function was made for the Honey Feeder, and I don't think it's needed anymore</remarks>
    internal static (int, int) ParseTimeString(this TimeOfDay timeOfDay, string timeString) 
    {
        return (int.Parse(timeString.Split(":")[0]), int.Parse(timeString.Split(":")[1]));
    }

    /// <summary>
    /// Checks if current time has passed specified target time
    /// </summary>
    /// <param name="timeOfDay">TimeOfDay instance being extended</param>
    /// <param name="target">Target time as (hours, minutes) tuple</param>
    /// <returns>True if current time matches or exceeds target time</returns>
    /// <remarks>This function was made for the Honey Feeder, and I don't think it's needed anymore</remarks>
    internal static bool HasPassedTime(this TimeOfDay timeOfDay, (int, int) target) 
    {
        return timeOfDay.HasPassedTime(timeOfDay.GetCurrentTime(), target);
    }
    
    /// <summary>
    /// Compares two time tuples to determine if target time has been passed
    /// </summary>
    /// <param name="timeOfDay">TimeOfDay instance being extended</param>
    /// <param name="current">Current time as (hours, minutes) tuple</param>
    /// <param name="target">Target time to check against</param>
    /// <returns>
    /// True if target hour is less than or equal to current hour AND
    /// target minute is less than or equal to current minute
    /// </returns>
    /// <remarks>This function was made for the Honey Feeder, and I don't think it's needed anymore</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasPassedTime(this TimeOfDay timeOfDay, (int, int) current, (int, int) target) 
    {
        return target.Item1 <= current.Item1 && target.Item2 <= current.Item2;
    }
    
    /// <summary>
    /// Safely updates a NetworkVariable value if different from current value
    /// </summary>
    /// <typeparam name="T">Type implementing IEquatable</typeparam>
    /// <param name="networkVariable">NetworkVariable to update</param>
    /// <param name="newValue">New value to potentially set</param>
    /// <remarks>
    /// Prevents unnecessary network updates by checking equality before setting
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ChangeNetworkVar<T>(NetworkVariable<T> networkVariable, T newValue) where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
        {
            networkVariable.Value = newValue;
        }
    }

    /// <summary>
    /// Safely retrieves all loadable types from an assembly
    /// </summary>
    /// <param name="assembly">Assembly to inspect</param>
    /// <returns>Enumerable collection of valid types</returns>
    /// <exception cref="ArgumentNullException">Thrown if assembly is null</exception>
    /// <remarks>
    /// Handles ReflectionTypeLoadException by returning only valid types
    /// </remarks>
    internal static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) 
    {
        if(assembly == null) throw new ArgumentNullException(nameof(assembly));
        
        try 
        {
            return assembly.GetTypes();
        } 
        catch (ReflectionTypeLoadException ex) 
        {
            return ex.Types.Where(t => t != null);
        }
    }
    
    /// <summary>
    /// Reflection-based check for IsHost property
    /// It is used in patches using <see cref="Biodiversity.Util.Attributes.ModConditionalPatch"/> to get the value of <c>__instance.IsHost</c>
    /// </summary>
    /// <param name="instance">Object instance to inspect</param>
    /// <returns>Value of IsHost property if exists</returns>
    /// <remarks>
    /// Returns false if property doesn't exist. Uses null-forgiving operator.
    /// </remarks>
    internal static bool IsHostReflection(object instance)
    {
        return (bool)instance.GetType().GetProperty("IsHost")?.GetValue(instance)!;
    }

    /// <summary>
    /// Reflection-based check for IsServer property
    /// It is used in patches using <see cref="Biodiversity.Util.Attributes.ModConditionalPatch"/> to get the value of <c>__instance.IsServer</c>
    /// </summary>
    /// <param name="instance">Object instance to inspect</param>
    /// <returns>Value of IsServer property if exists</returns>
    /// <remarks>
    /// Returns false if property doesn't exist. Uses null-forgiving operator.
    /// </remarks>
    internal static bool IsServerReflection(object instance)
    {
        return (bool)instance.GetType().GetProperty("IsServer")?.GetValue(instance)!;
    }
    
    /// <summary>
    /// Converts an RGB color to HSV and returns the original RGB color.
    /// </summary>
    /// <param name="rgb">The RGB color to convert.</param>
    /// <param name="h">The hue component of the HSV color.</param>
    /// <param name="s">The saturation component of the HSV color.</param>
    /// <param name="v">The value component of the HSV color.</param>
    /// <returns>The original RGB color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color RGBToHSV(Color rgb, out float h, out float s, out float v)
    {
        Color.RGBToHSV(rgb, out h, out s, out v);
        return rgb;
    }

    /// <summary>
    /// Converts HSV color components to an RGB color.
    /// </summary>
    /// <param name="h">The hue component of the HSV color.</param>
    /// <param name="s">The saturation component of the HSV color.</param>
    /// <param name="v">The value component of the HSV color.</param>
    /// <returns>The RGB color corresponding to the given HSV components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color HSVToRGB(float h, float s, float v)
    {
        return Color.HSVToRGB(h, s, v);
    }
}
