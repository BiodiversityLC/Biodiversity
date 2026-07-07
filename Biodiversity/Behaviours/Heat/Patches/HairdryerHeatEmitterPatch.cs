using Biodiversity.Core.Attributes;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat.Patches;

[CreaturePatch("WaxSoldier")] // This may be removed in the future if something else needs to use heat stuff
[HarmonyPatch(typeof(NoisemakerProp))]
internal static class HairdryerHeatEmitterPatch
{
    private const float WIDTH = 30f;
    private const float RANGE = 8f;
    private const float HEAT_IMPULSE = 5f;
    private static readonly int EnemyLayerMask = 1 << LayerMask.NameToLayer("Enemies");

    private static readonly Collider[] _buffer = new Collider[32];

    [HarmonyPatch(nameof(NoisemakerProp.ItemActivate))]
    [HarmonyPostfix]
    private static void EmitHeatOnActivate(NoisemakerProp __instance, bool used, bool buttonDown = true)
    {
        if (__instance.itemProperties.name != "Hairdryer") return;
        if (!HeatController.HasInstance) return;
        if (!__instance.playerHeldBy) return;

        // When an item is used, the UseItemOnClient() function is called, where it checks if the item has enough charge
        // in the batteries to use it. If it does, then ItemActivate() is called. Therefore, we don't need to check
        // the battery in this function; we can just apply the heat straight away.

        // The UseItemOnClient function calls a Server RPC that calls ItemActivate if and only if itemProperties.syncUseFunction is true.
        if (!__instance.itemProperties.syncUseFunction)
        {
            BiodiversityPlugin.Logger.LogWarning($"[{nameof(HairdryerHeatEmitterPatch)}] Cannot apply heat because itemProperties.syncUseFunction is false, meaning that any action taken here will not be synced to clients.");
            return;
        }

        Vector3 playerCameraPos = __instance.playerHeldBy.gameplayCamera.transform.position;
        int collidersFound = Physics.OverlapSphereNonAlloc(playerCameraPos, RANGE, _buffer, EnemyLayerMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < collidersFound; i++)
        {
            Collider col = _buffer[i];
            if (!col.TryGetComponent(out HeatSensor heatSensor))
                continue;

            Vector3 directionToSensor = col.bounds.center - playerCameraPos;
            float distanceToSensor = directionToSensor.magnitude;
            if (distanceToSensor > RANGE)
                continue;

            Vector3 directionToSensorNormal = directionToSensor / distanceToSensor;

            float cosHalfAngle = Mathf.Cos(Mathf.Clamp(WIDTH, 0f, 180f) * 0.5f * Mathf.Deg2Rad);
            if (Vector3.Dot(directionToSensorNormal,
                    __instance.playerHeldBy.gameplayCamera.transform.forward.normalized) < cosHalfAngle)
                continue; // Outside the view range

            // Don't heat through walls
            if (Physics.Linecast(col.bounds.center, playerCameraPos,
                    StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                continue;

            float falloff = 1f - distanceToSensor / RANGE;
            heatSensor.AddHeatImpulse(HEAT_IMPULSE * falloff);
        }
    }
}