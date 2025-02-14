﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Util;
internal static class ExtensionMethods 
{
    internal static Vector3 Direction(this Vector3 from, Vector3 to) 
    {
        return (to - from).normalized;
    }

    [SuppressMessage("ReSharper", "PossibleLossOfFraction", Justification = "Disabled message because Rider was complaining about totalMinutes / 60")]
    internal static (int hours, int minutes) GetCurrentTime(this TimeOfDay timeOfDay) 
    {
        int totalMinutes = Mathf.FloorToInt(timeOfDay.normalizedTimeOfDay * 60f * timeOfDay.numberOfHours + 360);
        int hour = Mathf.FloorToInt(totalMinutes / 60);

        return (hour, totalMinutes % 60);
    }
    
    internal static (int, int) ParseTimeString(this TimeOfDay timeOfDay, string timeString) 
    {
        return (int.Parse(timeString.Split(":")[0]), int.Parse(timeString.Split(":")[1]));
    }

    internal static bool HasPassedTime(this TimeOfDay timeOfDay, (int, int) target) 
    {
        return timeOfDay.HasPassedTime(timeOfDay.GetCurrentTime(), target);
    }
    
    internal static bool HasPassedTime(this TimeOfDay timeOfDay, (int, int) current, (int, int) target) 
    {
        return target.Item1 <= current.Item1 && target.Item2 <= current.Item2;
    }
    
    public static void ChangeNetworkVar<T>(NetworkVariable<T> networkVariable, T newValue) where T : IEquatable<T>
    {
        if (!EqualityComparer<T>.Default.Equals(networkVariable.Value, newValue))
        {
            networkVariable.Value = newValue;
        }
    }

    internal static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) 
    {
        if(assembly == null) throw new ArgumentNullException(nameof(assembly));
        
        try 
        {
            return assembly.GetTypes();
        } 
        catch(ReflectionTypeLoadException ex) 
        {
            return ex.Types.Where(t => t != null);
        }
    }
}
