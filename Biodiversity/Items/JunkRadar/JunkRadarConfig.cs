using BepInEx.Configuration;
using Biodiversity.Core.Config;
using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar
{
    public class JunkRadarConfig(ConfigFile cfg) : BiodiverseConfigLoader<JunkRadarConfig>(cfg)
    {
        [field: Header("_General")]

        [field: Tooltip("Whether the Junk Radar and buried scraps are able to spawn.")]
        public bool Enabled { get; private set; } = true;


        [field: Tooltip("The chance in % for the Junk Radar to spawn on moons (100 is a guaranteed spawn).")]
        [field: Range(0, 100)]
        public int SpawnChance { get; private set; } = 80;


        [field: Tooltip("Comma separated list of moons names where the Junk Radar and buried scraps are able to spawn (use \"All\" to allow it on all moons).")]
        public string SpawnMoons { get; private set; } = "Experimentation,March,Artifice";
        internal readonly List<string> SpawnMoonsList = [];


        [field: Tooltip("The percentage of the buried scraps amount that spawns on the moon compared to the amount of normal inside scraps. This balances out the number of buried scraps based on how many facility's scraps spawns ; the bigger this number is, the more buried scraps will spawn.")]
        [field: Range(0, 200)]
        public int BuriedScrapsAmountPercentage { get; private set; } = 15;

        [field: Tooltip("The percentage of the buried scraps rarity that spawns on the moon based on the moon's routing price. The more the cost is, the more chance you will get to finding valuable buried items. This value allows to customize how the moon's price balances the scraps rarities ; reducing this value will make it less likely to find rare buried scraps.")]
        [field: Range(0, 60)]
        public int BuriedScrapsRarityPercentage { get; private set; } = 15;


        [field: Header("Radar Item")]

        [field: Tooltip("The max distance at which the Junk Radar can detect underground buried scraps.")]
        [field: Range(1, 200)]
        public int MaxDetectionDistance { get; private set; } = 70;


        [field: Header("Buried Scraps")]

        [field: Tooltip("If enabled, the songs of the Ogopogo Trophy item are copyright free.")]
        public bool OgopogoTrophyIsCopyrightFree { get; private set; } = true;
    }
}
