using HarmonyLib;

namespace Biodiversity.Behaviours.Heat.Patches;

[HarmonyPatch(typeof(GrabbableObject))]
internal static class GrabbableObjectHeatEmitterPatches
{
    [HarmonyPatch(nameof(GrabbableObject.Start))]
    [HarmonyPostfix]
    private static void AttachHeatEmitterToGrabbableObject(GrabbableObject __instance)
    {
        if (HeatController.HasInstance)
        {
            HeatController.Instance.TryAttachEmitter(__instance.gameObject);
        }
    }
}