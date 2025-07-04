using UnityEngine;

namespace Biodiversity.Util;

public static class DebugUtils
{
    public static void DrawSphereCast(Ray ray, float radius, Color colour, float duration = 10f, float maxDistance = Mathf.Infinity, bool depthTest = true, int segments = 16)
    {
        Vector3 origin = ray.origin;
        if (maxDistance < Mathf.Epsilon) return;
        Vector3 direction = ray.direction.normalized;
        Vector3 end = origin + direction * maxDistance;

        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up);
        //if (perpendicular.sqrMagnitude < 0.001f) 
    }
}