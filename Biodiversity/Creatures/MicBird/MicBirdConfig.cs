using BepInEx.Configuration;
using Biodiversity.Util.Config;
using UnityEngine;

namespace Biodiversity.Creatures.MicBird
{
    public class MicBirdConfig(ConfigFile configFile) : BiodiverseConfigLoader<MicBirdConfig>(configFile) {
        [field: Tooltip("Spawn weight of the Boom bird on all moons.")]
        public string BoomBirdRarity { get; private set; } = "Experimentation:22,Adamance:15,Rend:5,Artifice:45,Atlantica:27,Fission-C:24,Gratar:10,Polarus:8,Seichi:8,Arelion:15,Fray:1,Sierra:21,Icebound:12,Humidity:38,Integrity:8,Vertigo:16,Vaporization:20,Timbrance:50,Rorm:25,Starship-13:25,Filitrios:35,Cubatres:10,Dirge:10,Kanie:5,Bilge:10,Acheron:5,Chronos:8";

        [field: Tooltip("Power level of the Boom bird")]
        public float PowerLevel { get; private set; } = 1;

        [field: Tooltip("Whether the Boom bird will spawn in games.")]
        public bool EnableBoomBird { get; private set; } = true;

        [field: Tooltip("Minimum time between roam/idle sounds")]
        public int BoomBirdIdleMinTime { get; private set; } = 15;

        [field: Tooltip("Maximum time between roam/idle sounds")]
        public int BoomBirdIdleMaxTime { get; private set; } = 35;

        [field: Tooltip("The stop distance for the radar boosters.")]
        public float RadarBoosterStopDistance { get; private set; } = 3f;

        // Malfunction configs
        [field: Tooltip("Weight of the walkie malfunction")]
        public int WalkieMalfunctionWeight { get; private set; } = 10;

        [field: Tooltip("Weight of the door malfunction")]
        public int DoorMalfunctionWeight { get; private set; } = 10;

        [field: Tooltip("Weight of the radar malfunction")]
        public int RadarMalfunctionWeight { get; private set; } = 10;

        [field: Tooltip("Weight of the lights out malfunction")]
        public int LightsOutMalfunctionWeight { get; private set; } = 10;

        // Chance based malfunction configs
        [field: Tooltip("Chance (in percent) of canceling a teleport into the ship when a malfunction occurs.")]
        public int TeleportCancelChance { get; private set; } = 50;

        [field: Tooltip("Chance (in percent) of canceling an inverse teleport when a malfunction occurs.")]
        public int InverseTeleportCancelChance { get; private set; } = 25;

        //Misc
        [field: Tooltip("Audio volume in percent of the default volume.")]
        public int AudioVolume { get; private set; } = 100;

        [field: Tooltip("Compatablility mode GUIDs. Put GUIDs of mods that cause the Boom Bird not to path to the top of the ship in here.")]
        public string CompatabilityModeGuids { get; private set; } = "MelanieMelicious.2StoryShip,windblownleaves.problematicpilotry";
    }
}
