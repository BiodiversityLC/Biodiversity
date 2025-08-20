using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[DisallowMultipleComponent]
public class DirectedConeHeatEmitter : HeatEmitter
{
    [Header("Heat Settings")]
    public float centreRateCPerSec = 30f;  // °C/s at 1m on-axis
    public float range = 6f;
    public float innerAngle = 20f;  // full power inside this cone
    [Range(1f, 80f)] public float outerAngle = 45f;  // fades to 0 by this angle
    public AnimationCurve falloff = AnimationCurve.Linear(0,1, 1,0);
    public Transform emitterCentre;

    [Range(8, 64)] public int segments = 24;
    
    private MeshCollider meshCollider;
    private Mesh coneMesh;

    private void OnEnable()
    {
        EnsureCollider(); 
        RebuildMesh(); 
    }
    
    private void Start()
    {
        emitterCentre ??= transform;
    }

    public override float GetHeatRateAt(Vector3 targetPos)
    {
        // Quick reject if outside cone range
        Vector3 local = transform.InverseTransformPoint(targetPos);
        if (local.z <= 0f || local.z > range) return 0f;

        float radiusAtZ = Mathf.Tan(0.5f * Mathf.Deg2Rad * outerAngle) * local.z;
        float radial = new Vector2(local.x, local.y).magnitude;
        if (radial > radiusAtZ) return 0f; // Outside cone range

        // Angular weighting (innerAngle gets full power)
        float ang = Mathf.Atan2(radial, Mathf.Max(1e-4f, local.z)) * Mathf.Rad2Deg;
        float angT = Mathf.InverseLerp(outerAngle, innerAngle, ang);
        float angular = Mathf.Clamp01(angT);

        // Distance falloff along the axis
        float distT = Mathf.Clamp01(local.z / range);
        float axial = falloff.Evaluate(1f - distT);

        // 1m normalization
        float norm = 1f / (1f + local.sqrMagnitude);
        return centreRateCPerSec * angular * axial * norm;
    }

    private void EnsureCollider()
    {
        meshCollider ??= GetComponent<MeshCollider>();
        meshCollider ??= gameObject.AddComponent<MeshCollider>();
        meshCollider.convex = true;
        meshCollider.isTrigger = true;
    }

    private void RebuildMesh()
    {
        float halfRad = 0.5f * Mathf.Deg2Rad * outerAngle;
        float radius = Mathf.Tan(halfRad) * range;

        if (!coneMesh)
        {
            coneMesh = new Mesh { name = "ConeTriggerMesh" };
#if UNITY_EDITOR
            // Prevent saving an ever-growing mesh to the scene file in editor
            coneMesh.hideFlags = HideFlags.DontSave;
#endif
        }
        coneMesh.Clear();

        // Build a closed cone whose apex is at (0,0,0) and base circle at z = length
        int vCount = 1 + segments + 1; // apex + ring + centre of base
        Vector3[] v = new Vector3[vCount];
        Vector3 apex = Vector3.zero;
        v[0] = apex;

        // Base ring
        for (int i = 0; i < segments; i++)
        {
            float ang = i * Mathf.PI * 2f / segments;
            v[1 + i] = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, range);
        }
        
        // Base centre
        v[1 + segments] = new Vector3(0f, 0f, range);

        // Triangles
        int sideTris = segments; // Apex -> i -> i+1
        int capTris  = segments; // Centre -> i+1 -> i
        int[] t = new int[(sideTris + capTris) * 3];
        int ti = 0;

        // sides
        for (int i = 0; i < segments; i++)
        {
            const int i0 = 0; // Apex
            int i1 = 1 + i;
            int i2 = 1 + (i + 1) % segments;
            t[ti++] = i0; t[ti++] = i1; t[ti++] = i2;
        }
        
        // Base cap (clockwise to face outward in +Z)
        int c = 1 + segments; // Base centre
        for (int i = 0; i < segments; i++)
        {
            int i1 = 1 + i;
            int i2 = 1 + (i + 1) % segments;
            t[ti++] = c; t[ti++] = i2; t[ti++] = i1;
        }

        coneMesh.vertices = v;
        coneMesh.triangles = t;
        coneMesh.RecalculateNormals();
        coneMesh.RecalculateBounds();

        meshCollider.sharedMesh = coneMesh;
    }
}