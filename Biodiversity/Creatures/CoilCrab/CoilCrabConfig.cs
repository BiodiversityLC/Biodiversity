using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilCrabConfig(ConfigFile configFile) : BiodiverseConfigLoader<CoilCrabConfig>(configFile)
    {
        [field: Tooltip("Whether the Coil-Crab will spawn in games")]
        public bool EnableCoilCrab { get; private set; } = true;

        [field: Tooltip("Spawn weight of the Coil Crab on all moons when it is stormy.")]
        public string CoilCrabRarityStormy { get; private set; } = "Offense:75,Rend:50,Titan:25,Artifice:35,Asteroid-13:15,Junic:29,Atlantica:15,Gloom:5,Gratar:53,Acidir:12,Fission:28,Oldred:37,Prominence:20,Seichi:8,Torus:72,Tundaria:39,Starship-13:22";

        [field: Tooltip("Spawn weight of the Coil Crab on all moons when it is not stormy.")]
        public string CoilCrabRarity { get; private set; } = "Offense:35,Rend:8,Titan:0,Artifice:12,Asteroid-13:9,Junic:18,Atlantica:2,Gloom:4,Gratar:20,Acidir:10,Fission:20,Oldred:20,Prominence:10,Seichi:4,Torus:38,Tundaria:19,Starship-13:22";

        [field: Tooltip("Explosion damage range")]
        public float DamageRange { get; private set; } = 6;

        [field: Tooltip("Explosion kill range")]
        public float KillRange { get; private set; } = 3;

        [field: Tooltip("Explosion damage.")]
        public int ExplosionDamage { get; private set; } = 50;

        [field: Tooltip("How fast the crab will move when creeping")]
        public float CreepSpeed { get; private set; } = 4.5f;

        [field: Tooltip("How fast the crab will move when running towards the player during the explosion state")]
        public float RunSpeed { get; private set; } = 5;

        [field: Tooltip("Health value of the Coil-Crab")]
        public int Health { get; private set; } = 3;


        [field: Tooltip("Item value. format: Min:(minimum number),Max:(maximum number)")]
        public string ItemValue { get; private set; } = "Min:60,Max:95";
    }
}
