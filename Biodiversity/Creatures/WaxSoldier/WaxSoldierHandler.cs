namespace Biodiversity.Creatures.WaxSoldier;

internal class WaxSoldierHandler : BiodiverseAIHandler<WaxSoldierHandler>
{
    internal WaxSoldierAssets Assets { get; set; }

    public WaxSoldierHandler()
    {
        // Assets = new WaxSoldierAssets("waxsoldier");
        // LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Assets.WaxSoldierType.enemyPrefab);
        // LethalLib.Modules.Enemies.RegisterEnemy(Assets.WaxSoldierType, 0, LethalLib.Modules.Levels.LevelTypes.All,Assets.WaxSoldierNode,Assets.WaxSoldierKey);
    }
}