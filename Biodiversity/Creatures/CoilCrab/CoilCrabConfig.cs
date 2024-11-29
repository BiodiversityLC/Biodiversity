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
        [field: Tooltip("Whether the Coil-Crab will spawn in games.")]
        public bool EnableCoilCrab { get; private set; } = false;

        [field: Tooltip("Explosion damage range")]
        public float DamageRange { get; private set; } = 6;

        [field: Tooltip("How fast the crab will move when creeping")]
        public float CreepSpeed { get; private set; } = 4.5f;

        [field: Tooltip("How fast the crab will move when running towards the player during the explosion state")]
        public float RunSpeed { get; private set; } = 5;
    }
}
