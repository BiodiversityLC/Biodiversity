using Biodiversity.Core.Attributes;
using HarmonyLib;
using Unity.Netcode;

namespace Biodiversity.Behaviours.Heat.Patches;

[CreaturePatch("WaxSoldier")] // This may be removed in the future if something else needs to use heat stuff
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