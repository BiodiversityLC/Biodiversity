using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.HoneyFeeder;
[Serializable]
internal class HoneyFeederConfig(ConfigFile configFile) : BiodiverseConfig<HoneyFeederConfig>(configFile)
{

}
