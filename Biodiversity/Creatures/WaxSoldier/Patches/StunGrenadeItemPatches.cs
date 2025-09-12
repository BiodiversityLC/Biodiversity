using Biodiversity.Core.Attributes;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Patches;

[CreaturePatch("WaxSoldier")]
[HarmonyPatch(typeof(StunGrenadeItem))]
internal static class StunGrenadeItemPatches
{
    [HarmonyPatch(nameof(StunGrenadeItem.StunExplosion))]
    private static void HeatWaxOnStunGrenadeExplosion(Collider[] colliderArray, float enemyStunTime)
    {
        if (colliderArray == null || colliderArray.Length == 0 || enemyStunTime <= 0)
        {
            return;
        }

        for (int i = 0; i < colliderArray.Length; i++)
        {
            BiodiversityPlugin.LogVerbose($"[HeatWaxOnStunGrenadeExplosion] Found collider: {colliderArray[i].name}");
            if (colliderArray[i].TryGetComponent(out EnemyAICollisionDetect collisionDetect))
            {
                if (collisionDetect.mainScript is WaxSoldierAI waxSoldier)
                {
                    // tune these properly
                    waxSoldier.heatSensor.AddHeatImpulse(40);
                }
            }
        }
    }
}