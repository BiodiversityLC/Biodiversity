using BepInEx.Configuration;
using Biodiversity.Core.Config;
using UnityEngine;

namespace Biodiversity.Creatures.CoilCrab;

internal class CoilCrabConfig(ConfigFile configFile) : BiodiverseConfigLoader<CoilCrabConfig>(configFile)
{
    [field: Tooltip("Whether the Coil-Crab will spawn in games")]
    public bool EnableCoilCrab { get; private set; } = true;

    [field: Tooltip("Spawn weight of the Coil Crab on all moons when it is stormy.")]
    public string CoilCrabRarityStormy { get; private set; } = "Offense:120,Rend:10,Titan:25,Artifice:45,Asteroid-13:15,Junic:29,Atlantica:15,Gratar:53,Fission:28,Oldred:37,Prominence:40,Seichi:8,Torus:82,Tundaria:39,Starship-13:22,Demetrica:40,Collateral:80,Humidity:21,Brutality:65,Devastation:105,Vaporization:60,Burrow:80,Anchorage:45,Pelagia:85,Bilge:30,Acheron:75,Chronos:54,Consolidation:12";

    [field: Tooltip("Spawn weight of the Coil Crab on all moons when it is not stormy.")]
    public string CoilCrabRarity { get; private set; } = "Offense:75,Rend:8,Titan:0,Artifice:12,Asteroid-13:9,Junic:18,Atlantica:2,Gratar:8,Fission:20,Oldred:20,Prominence:10,Seichi:4,Torus:38,Tundaria:19,Starship-13:22,Demetrica:10,Collateral:8,Humidity:0,Brutality:25,Devastation:65,Vaporization:35,Burrow:20,Anchorage:5,Pelagia:15,Bilge:10,Acheron:25,Chronos:18,Consolidation:30";

    [field: Tooltip("Power level of the Coil Crab")]
    public float PowerLevel { get; private set; } = 1;

    [field: Tooltip("The number of maximum Coil Crab spawns.")]
    public int MaxSpawns { get; private set; } = 5;

    [field: Tooltip("Explosion damage range")]
    public float DamageRange { get; private set; } = 7;

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
    public string ItemValue { get; private set; } = "Min:42,Max:75";
}
