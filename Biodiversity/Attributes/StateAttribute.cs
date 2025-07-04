using System;

namespace Biodiversity.Util.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal class StateAttribute : Attribute
{
    internal object StateType { get; }

    internal StateAttribute(object stateType)
    {
        StateType = stateType;
    }
}