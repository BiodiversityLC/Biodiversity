using BepInEx.Configuration;
using Biodiversity.Core.Config;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarConfig(ConfigFile cfg) : BiodiverseConfigLoader<JunkRadarConfig>(cfg)
    {
        [field: Header("General")]

        [field: Tooltip("Whether the Junk Radar and buried scraps are able to spawn.")]
        public bool Enabled { get; private set; } = true;


        [field: Tooltip("The chance in % for the Junk Radar to spawn on moons (100 is a guaranteed spawn).")]
        [field: Range(0, 100)]
        public int SpawnChance { get; private set; } = 80;


        [field: Tooltip("Comma separated list of moons names where the Junk Radar and buried scraps are able to spawn (use \"All\" to allow it on all moons).")]
        public string SpawnMoons { get; private set; } = "Experimentation,March,Artifice";
        internal readonly List<string> SpawnMoonsList = [];


        [field: Tooltip("The min,max amount of buried scraps that will spawn on moons (if the Junk Radar is spawned).")]
        public string BuriedScrapsAmountMinMax { get; private set; } = "5,7";


        [field: Header("Radar Item")]

        [field: Tooltip("The max distance at which the Junk Radar can detect underground buried scraps.")]
        [field: Range(1, 200)]
        public int MaxDetectionDistance { get; private set; } = 70;
    }
}
