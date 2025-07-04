using UnityEngine;

namespace Biodiversity.Util;

public static class DebugUtils
{
    public static void DrawSphereCast(
        Ray ray, 
        float radius, 
        Color colour, 
        float duration = 10f, 
        float maxDistance = Mathf.Infinity, 
        bool depthTest = true, 
        int segments = 16)
    {
        Vector3 origin = ray.origin;
        if (maxDistance < Mathf.Epsilon) return;
        Vector3 direction = ray.direction.normalized;
        Vector3 end = origin + direction * maxDistance;

        // Create basis vectors (direction, perpendicular, perpendicular2)
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up);
        if (perpendicular.sqrMagnitude < 0.001f)
            perpendicular = Vector3.Cross(direction, Vector3.right);
        perpendicular.Normalize();
        Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular).normalized;
        
        // Draw the two end-circles
        for (int i = 0; i < segments; i++)
        {
            float a0 = 2 * Mathf.PI * i / segments;
            float a1 = 2 * Mathf.PI * (i + 1) / segments;
            Vector3 offset0 = perpendicular * (Mathf.Cos(a0) * radius) + perpendicular2 * (Mathf.Sin(a0) * radius);
            Vector3 offset1 = perpendicular * (Mathf.Cos(a1) * radius) + perpendicular2 * (Mathf.Sin(a1) * radius);

            Debug.DrawLine(origin + offset0, origin + offset1, colour, duration, depthTest);
            Debug.DrawLine(end + offset0, end + offset1, colour, duration, depthTest);
        }
        
        // Draw the cylinder edges at 4 cardinal points
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.PI * 0.5f * i;
            Vector3 offset = perpendicular * (Mathf.Cos(a) * radius) + perpendicular2 * (Mathf.Sin(a) * radius);
            Debug.DrawLine(origin + offset, end + offset, colour, duration, depthTest);
        }
    }
}