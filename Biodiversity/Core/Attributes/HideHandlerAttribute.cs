using System;

namespace Biodiversity.Core.Attributes;

// Stick this on a handler class to hide it from the config (and disable it as a consequence)
[AttributeUsage(AttributeTargets.Class)]
public class HideHandlerAttribute : Attribute;