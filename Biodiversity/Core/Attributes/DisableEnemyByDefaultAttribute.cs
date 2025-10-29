using System;

namespace Biodiversity.Core.Attributes;

// Stick this on a creature handler class to disable it by default in the config
[AttributeUsage(AttributeTargets.Class)]
public class DisableEnemyByDefaultAttribute : Attribute;