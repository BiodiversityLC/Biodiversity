using UnityEngine;

namespace Biodiversity.Util;

public static class LineOfSightUtil
{
    /// <summary>
    /// Determines if there is an unobstructed line of sight to a position within a specified view cone and range.
    /// </summary>
    /// <param name="targetPosition">The position to check line of sight to.</param>
    /// <param name="eyeTransform">The transform representing the eye's position and forward direction.</param>
    /// <param name="viewWidth">The total angle of the view cone in degrees.</param>
    /// <param name="viewRange">The maximum distance for the check.</param>
    /// <param name="proximityAwareness">The proximity awareness range. If the value is less than zero, then it is assumed that there is no proximity awareness at all.</param>
    /// <param name="isFoggy">Whether the AI is outside in a foggy environment, and it cannot see through fog (<see cref="EnemyType.canSeeThroughFog"/>).</param>
    /// <returns>Returns <c>true</c> if the AI has line of sight to the given position; otherwise, <c>false</c>.</returns>
    internal static bool HasLineOfSight(
        Vector3 targetPosition,
        Transform eyeTransform,
        float viewWidth = 45f,
        float viewRange = 60f,
        float proximityAwareness = -1f,
        bool isFoggy = false)
    {
        // LogVerbose($"In {nameof(HasLineOfSight)}");

        if (!eyeTransform) return false;
        
        Vector3 eyePosition = eyeTransform.position;
        Vector3 directionToTarget = targetPosition - eyePosition;
        float sqrDistance = directionToTarget.sqrMagnitude;
        
        // If the target is directly on top of you, then treat them as visible
        if (sqrDistance <= 0.0001f) return true;
        
        // 1). Get effective range by taking fog into account
        float effectiveRange = viewRange;
        if (isFoggy)
        {
            effectiveRange = Mathf.Clamp(viewRange, 0f, 30f);
        }
        
        // 2). Range check
        float effectiveRangeSqr = effectiveRange * effectiveRange;
        if (sqrDistance > effectiveRangeSqr)
        {
            // LogVerbose($"Distance check failed: {Mathf.Sqrt(sqrDistance)} (distance) > {effectiveRange} (effectiveRange)");
            return false;
        }
        
        // 3). FOV check. The proximity can bypass the FOV check, but not the physics obstruction check.
        float distance = Mathf.Sqrt(sqrDistance);
        if (!(proximityAwareness >= 0f && distance <= proximityAwareness))
        {
            float halfFov = Mathf.Clamp(viewWidth, 0f, 180f) * 0.5f * Mathf.Deg2Rad;
            float cosHalfFov = Mathf.Cos(halfFov);
            float dotProduct = Vector3.Dot(eyeTransform.forward, directionToTarget / distance);
            if (dotProduct < cosHalfFov)
            {
                // LogVerbose($"FOV check failed: {dotProduct} (dotProduct) < {cosHalfFov} (cosHalfFov)");
                return false;
            }
        }
        
        // 4). Obstruction check
        if (Physics.Linecast(eyePosition, targetPosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            // LogVerbose("Line of sight check failed");
            return false;
        }

        return true;
    }
}