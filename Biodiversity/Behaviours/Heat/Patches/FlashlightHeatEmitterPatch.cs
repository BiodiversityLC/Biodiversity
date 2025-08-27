using HarmonyLib;
using Unity.Netcode;

namespace Biodiversity.Behaviours.Heat.Patches;

[HarmonyPatch(typeof(FlashlightItem))]
internal static class FlashlightHeatEmitterPatch
{
    [HarmonyPatch(nameof(FlashlightItem.SwitchFlashlight))]
    [HarmonyPostfix]
    private static void ToggleFlashlight(FlashlightItem __instance, bool on)
    {
        // SwitchFlashlight is synced with the server
        if (!NetworkManager.Singleton.IsServer || !HeatController.HasInstance) return;

        HeatEmitter emitter = HeatController.Instance.GetEmitterForComponent(__instance);
        if (emitter && emitter.enabled != on)
        {
            // BiodiversityPlugin.LogVerbose($"Flashlight on: {on}.");
            emitter.enabled = on;
        }
    }

    [HarmonyPatch(nameof(FlashlightItem.PocketItem))]
    [HarmonyPrefix]
    private static void PocketFlashlightToggle(FlashlightItem __instance)
    {
        if (!NetworkManager.Singleton.IsServer || !HeatController.HasInstance) return;
        
        HeatEmitter emitter = HeatController.Instance.GetEmitterForComponent(__instance);
        if (emitter)
        {
            emitter.enabled = false;
        }
    }
}