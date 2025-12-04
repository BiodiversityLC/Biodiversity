using BepInEx.Configuration;
using Biodiversity.Core.Config;
using UnityEngine;

namespace Biodiversity.Creatures.Rock
{
    internal class RockConfig(ConfigFile configFile) : BiodiverseConfigLoader<RockConfig>(configFile)
    {
        [field: Tooltip("Whether the Rock will spawn in games.")]
        public bool RockEnabled { get; private set; } = true;

        [field: Tooltip("Spawn weight of Rock on all moons.")]
        public string RockRarity { get; private set; } = "Rend:55,Titan:25";

        [field: Tooltip("What material to use for each level. (0 is brown, 1 is grey)")]
        public string RockMatsConfig { get; private set; } = "ExperimentationLevel:0,AssuranceLevel:0,OffenseLevel:0,VowLevel:1,MarchLevel:1,AdamanceLevel:1,RendLevel:1,DineLevel:1,TitanLevel:1,ArtificeLevel:1";
    }
}
