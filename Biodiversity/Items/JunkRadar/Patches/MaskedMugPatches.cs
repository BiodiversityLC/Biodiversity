using Biodiversity.Items.JunkRadar.BuriedScrap;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Items.JunkRadar.Patches
{
    [HarmonyPatch]
    internal class MaskedMugPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "Start")]
        public static void GetMaskedReferenceForBuriedMugPatch(Terminal __instance)
        {
            if (JunkRadarHandler.Instance.Config.Enabled && MaskedMugItem.enemyToSpawn != null)
            {
                return;
            }
            foreach (SelectableLevel level in __instance.moonsCatalogueList)
            {
                foreach (SpawnableEnemyWithRarity enemy in level.Enemies)
                {
                    if (enemy.enemyType.enemyName == "Masked" && MaskedMugItem.enemyToSpawn == null)
                    {
                        MaskedMugItem.enemyToSpawn = enemy;
                        return;
                    }
                }
            }
        }


        private static MaskedMugItem maskedItemToSpawnEnemy = null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        public static void TrySpawnMaskedEnemyPrefix(PlayerControllerB __instance)
        {
            if (__instance == null || __instance.isPlayerDead)
            {
                return;
            }
            foreach (GrabbableObject item in __instance.ItemSlots)
            {
                if (item != null && item is MaskedMugItem maskedItem && !maskedItem.isEnemySpawning)
                {
                    maskedItem.isEnemySpawning = true;
                    maskedItemToSpawnEnemy = maskedItem;
                    return;
                }
            }
            if (__instance.ItemOnlySlot != null && __instance.ItemOnlySlot is MaskedMugItem maskedItemUtility && !maskedItemUtility.isEnemySpawning)
            {
                maskedItemUtility.isEnemySpawning = true;
                maskedItemToSpawnEnemy = maskedItemUtility;
                return;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        public static void TrySpawnMaskedEnemyPostfix(PlayerControllerB __instance)
        {
            if (__instance == null || !__instance.isPlayerDead || maskedItemToSpawnEnemy == null)
            {
                maskedItemToSpawnEnemy = null;
                return;
            }
            maskedItemToSpawnEnemy.SpawnMaskedEnemyServerRpc(__instance.playerClientId, __instance.transform.position);
            maskedItemToSpawnEnemy = null;
        }
    }
}
