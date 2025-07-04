using BepInEx.Configuration;
using Biodiversity.Core.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Beetler;

internal class BeetlerConfig(ConfigFile configFile) : BiodiverseConfigLoader<BeetlerConfig>(configFile)
{
    [field: Tooltip("Whether the Beetler will appear. (Enable at your own risk!)")]
    public bool EnableBeetler { get; private set; } = false;
}