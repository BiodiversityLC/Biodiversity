namespace Biodiversity.Items.JunkRadar
{
    internal class JunkRadarHandler : BiodiverseItemHandler<JunkRadarHandler>
    {
        internal JunkRadarAssets Assets { get; set; }
        internal JunkRadarConfig Config { get; set; }

        public JunkRadarHandler()
        {
            Assets = new JunkRadarAssets("biodiversity_junkradar");
            Config = new JunkRadarConfig(BiodiversityPlugin.Instance.CreateConfig("junk_radar"));

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.BuriedScrapPrefab);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.OldVaseItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.OldVaseItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.MotherboardItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.MotherboardItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.CoilCrabItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.CoilCrabItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.JunkRadarItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.JunkRadarItem);
        }
    }
}
