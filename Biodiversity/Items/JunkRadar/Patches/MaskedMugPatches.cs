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
            if (!JunkRadarHandler.Instance.Config.Enabled || MaskedMugItem.enemyToSpawn != null)
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
        public static void TrySpawnMaskedEnemyPrefix(PlayerControllerB __instance, ref bool spawnBody)
        {
            if (!JunkRadarHandler.Instance.Config.Enabled || __instance == null || __instance.isPlayerDead)
            {
                return;
            }
            System.Func<GrabbableObject, bool> testItemIsMaskedMug = new((item) =>
            {
                if (item != null && item is MaskedMugItem maskedItem)
                {
                    maskedItem.lastPlayerIsInFactoryOnDeath = __instance.isInsideFactory;
                    maskedItem.lastPlayerRotationYOnDeath = __instance.transform.eulerAngles.y;
                    maskedItemToSpawnEnemy = maskedItem;
                    return true;
                }
                return false;
            });
            foreach (GrabbableObject item in __instance.ItemSlots)
            {
                if (testItemIsMaskedMug(item))
                {
                    spawnBody = true;
                    return;
                }
            }
            if (testItemIsMaskedMug(__instance.ItemOnlySlot))
            {
                spawnBody = true;
                return;
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        public static void TrySpawnMaskedEnemyPostfix(PlayerControllerB __instance)
        {
            if (!JunkRadarHandler.Instance.Config.Enabled || __instance == null || !__instance.isPlayerDead || maskedItemToSpawnEnemy == null)
            {
                maskedItemToSpawnEnemy = null;
                return;
            }
            if (maskedItemToSpawnEnemy.IsServer)
            {
                maskedItemToSpawnEnemy.SpawnMaskedEnemyServerRpc(__instance.playerClientId, __instance.transform.position);
            }
            maskedItemToSpawnEnemy = null;
        }
    }
}
