using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.MicBird
{
    public class MicBirdConfig(ConfigFile configFile) : BiodiverseConfigLoader<MicBirdConfig>(configFile) {
        [field: Tooltip("Spawn weight of the Boom bird on all moons.")]
        public string BoomBirdRarity { get; private set; } = "Experimentation:15,Rend:32,Titan:15,Artifice:36,Atlantica:27,Etern:18,Fission-C:24,Gratar:10,Polarus:8,Seichi:8,Olympus:4,Arelion:5,Fray:1,Sierra:21,Icebound:12,Humidity:26,Integrity:8,Vertigo:16,Vaporization:20";

        [field: Tooltip("Whether the Boom bird will spawn in games.")]
        public bool EnableBoomBird { get; private set; } = true;

        [field: Tooltip("Minimum time between roam/idle sounds")]
        public int BoomBirdIdleMinTime { get; private set; } = 15;

        [field: Tooltip("Maximum time between roam/idle sounds")]
        public int BoomBirdIdleMaxTime { get; private set; } = 35;
    }
}
