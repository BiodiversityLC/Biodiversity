using HarmonyLib;
using Unity.Netcode;

namespace Biodiversity.Behaviours.Heat.Patches;

[HarmonyPatch(typeof(GrabbableObject))]
internal static class GrabbableObjectHeatEmitterPatches
{
    [HarmonyPatch(nameof(GrabbableObject.Start))]
    [HarmonyPostfix]
    private static void AttachHeatEmitterToGrabbableObject(GrabbableObject __instance)
    {
        if (NetworkManager.Singleton.IsServer && HeatController.HasInstance)
        {
            HeatController.Instance.TryAttachEmitter(__instance.gameObject);
        }
    }
}