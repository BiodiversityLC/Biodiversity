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
        }
    }
}
