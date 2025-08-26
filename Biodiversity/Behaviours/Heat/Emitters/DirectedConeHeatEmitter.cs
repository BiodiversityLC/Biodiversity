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

    [Header("Mesh Settings")]
    [Range(8, 64)] public int segments = 24;
    
    [Header("Debug Settings")]
    [Tooltip("If true, a semi-transparent mesh will be rendered to show the cone's shape.")]
    public bool showDebugVisualizer;
    
    private Mesh _coneMesh;
    private MeshCollider _meshCollider;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private static Material _meshMaterial;

    private float _tanOuterAngle;
    private float _tanInnerAngle;
    
    private void OnValidate()
    {
        // Clamp inner angle to be less than outer angle to prevent invalid configurations
        innerAngle = Mathf.Min(innerAngle, outerAngle);
    }

    private void Awake()
    {
        showDebugVisualizer = true;
        
        emitterCentre ??= transform;
        PrecomputeAngles();
        EnsureCollider(); 
        RebuildMesh();
        SetupDebugVisualizer(showDebugVisualizer);
    }

    private void OnEnable()
    {
        if (_meshCollider)
        {
            _meshCollider.enabled = true;
        }

        if (_meshRenderer)
        {
            _meshRenderer.enabled = showDebugVisualizer;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        if (_meshCollider)
        {
            _meshCollider.enabled = false;
        }
        
        if (_meshRenderer)
        {
            _meshRenderer.enabled = false;
        }
    }

    public override float GetHeatRateAt(Vector3 targetPos)
    {
        // Quick reject if outside cone range
        Vector3 local = emitterCentre.InverseTransformPoint(targetPos);
        if (local.z <= 0f || local.z > range) return 0f;

        float radialSqr = local.x * local.x + local.y * local.y;
        float radiusAtZSqr = _tanOuterAngle * local.z;
        radiusAtZSqr *= radiusAtZSqr;
        if (radialSqr > radiusAtZSqr) return 0f; // Outside cone range

        // Angular weighting (innerAngle gets full power)
        float radial = Mathf.Sqrt(radialSqr);
        float tanOfCurrentAngle = radial / Mathf.Max(1e-4f, local.z);
        
        // Lerp between the precomputed tangents
        float angT = Mathf.InverseLerp(_tanOuterAngle, _tanInnerAngle, tanOfCurrentAngle);
        float angular = Mathf.Clamp01(angT);

        // Distance falloff along the axis
        float distT = local.z / range;
        float axial = falloff.Evaluate(distT);

        // 1m normalization
        float norm = 1f / (1f + local.sqrMagnitude);
        return centreRateCPerSec * angular * axial * norm;
    }
    
    private void PrecomputeAngles()
    {
        _tanOuterAngle = Mathf.Tan(0.5f * Mathf.Deg2Rad * outerAngle);
        _tanInnerAngle = Mathf.Tan(0.5f * Mathf.Deg2Rad * innerAngle);
    }

    private void EnsureCollider()
    {
        _meshCollider = gameObject.TryGetComponent(out MeshCollider collider) ? collider : gameObject.AddComponent<MeshCollider>();
        _meshCollider.convex = true;
        _meshCollider.isTrigger = true;
    }

    private void RebuildMesh()
    {
        float halfRad = 0.5f * Mathf.Deg2Rad * outerAngle;
        float radius = Mathf.Tan(halfRad) * range;

        if (!_coneMesh)
        {
            _coneMesh = new Mesh { name = "ConeTriggerMesh" };
#if UNITY_EDITOR
            // Prevent saving an ever-growing mesh to the scene file in editor
            coneMesh.hideFlags = HideFlags.DontSave;
#endif
        }
        _coneMesh.Clear();

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

        _coneMesh.vertices = v;
        _coneMesh.triangles = t;
        _coneMesh.RecalculateNormals();
        _coneMesh.RecalculateBounds();

        _meshCollider.sharedMesh = _coneMesh;
    }

    private void SetupDebugVisualizer(bool enable)
    {
        _meshFilter = gameObject.TryGetComponent(out MeshFilter meshFilter) ? meshFilter : gameObject.AddComponent<MeshFilter>();
        _meshRenderer = gameObject.TryGetComponent(out MeshRenderer meshRenderer) ? meshRenderer : gameObject.AddComponent<MeshRenderer>();

        if (!_meshMaterial)
        {
            Shader shader = Shader.Find("Sprites/Default");
            _meshMaterial = new Material(shader)
            {
                color = new Color(1f, 0.92f, 0.016f, 0.25f)
            };
        }

        _meshFilter.sharedMesh = _coneMesh;
        
        _meshRenderer.sharedMaterial = _meshMaterial;
        _meshRenderer.enabled = enable;
    }
}