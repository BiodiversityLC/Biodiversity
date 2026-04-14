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

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.JunkRadarItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.JunkRadarItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.BuriedScrapPrefab);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.OldVaseItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.OldVaseItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.MotherboardItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.MotherboardItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.CoilCrabItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.CoilCrabItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.BaboonSkullItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.BaboonSkullItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.SkullItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.SkullItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.MaskedMugItem.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.MaskedMugItem);

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.OgopogoTrophy.spawnPrefab);
            LethalLib.Modules.Items.RegisterItem(Assets.OgopogoTrophy);
        }
    }
}
