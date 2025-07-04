using System;

namespace Biodiversity.Util.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class CreaturePatchAttribute(string creatureName) : Attribute
{
    public string CreatureName { get; } = creatureName;
}