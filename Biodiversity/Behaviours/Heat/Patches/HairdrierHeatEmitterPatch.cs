using Biodiversity.Core.Attributes;
using HarmonyLib;

namespace Biodiversity.Behaviours.Heat.Patches;

[CreaturePatch("WaxSoldier")] // This may be removed in the future if something else needs to use heat stuff
[HarmonyPatch(typeof(NoisemakerProp))]
internal static class HairdrierHeatEmitterPatch
{
    [HarmonyPatch(nameof(NoisemakerProp.ItemActivate))]
    [HarmonyPostfix]
    private static void EmitHeatOnActivate(NoisemakerProp __instance, bool used, bool buttonDown = true)
    {
        if (!HeatController.HasInstance) return;

        // When an item is used, the UseItemOnClient() function is called, where it checks if the item has enough charge
        // in the batteries to use it. If it does, then ItemActivate() is called. Therefore, we don't need to check
        // the battery in this function; we can just apply the heat straight away.

        // The UseItemOnClient function calls a Server RPC that calls ItemActivate if and only if itemProperties.syncUseFunction is true

        // todo: do a cone-cast type thingy
    }
}