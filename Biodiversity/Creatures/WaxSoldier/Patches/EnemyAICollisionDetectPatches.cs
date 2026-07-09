using Biodiversity.Core.Attributes;
using GameNetcodeStuff;
using HarmonyLib;

namespace Biodiversity.Creatures.WaxSoldier.Patches;

/// <summary>
/// This class is for <see cref="EnemyAICollisionDetect"/> patches.
/// It makes the Wax Soldier (<see cref="WaxSoldierAI"/>) receive a large enough heat impulse to make it fully melt
/// when zapped by a zap gun (<see cref="PatcherTool"/>).
/// </summary>
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
            waxSoldier.LogVerbose($"[{nameof(MeltWaxSoldierFromZapGun)}] Applying zap gun heat impulse!");
            waxSoldier.heatSensor.AddHeatImpulse(500);
        }
    }
}