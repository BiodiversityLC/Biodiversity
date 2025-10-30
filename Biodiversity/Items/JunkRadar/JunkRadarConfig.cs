using BepInEx.Configuration;
using Biodiversity.Core.Config;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarConfig(ConfigFile cfg) : BiodiverseConfigLoader<JunkRadarConfig>(cfg)
    {
        /*[field: Header("config section")]

        [field: Tooltip("config description.")]
        public bool JunkConfig { get; private set; } = true;*/
    }
}
