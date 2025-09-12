using Biodiversity.Core.Attributes;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.WaxSoldier.Patches;

[CreaturePatch("WaxSoldier")]
[HarmonyPatch(typeof(EnemyAICollisionDetect))]
internal static class EnemyAICollisionDetectPatches
{
    [HarmonyPatch("IShockableWithGun.ShockWithGun", MethodType.Normal)]
    [HarmonyPostfix]
    private static void MeltWaxSoldierFromZapGun(EnemyAICollisionDetect __instance, PlayerControllerB shockedByPlayer)
    {
        if (__instance.mainScript is WaxSoldierAI waxSoldier)
        {
            waxSoldier.heatSensor.AddHeatImpulse(500);
        }
    }
}