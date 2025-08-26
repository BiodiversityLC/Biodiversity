using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[DisallowMultipleComponent]
public class RadialHeatEmitter : HeatEmitter
{
    [Header("Heat Settings")]
    [Tooltip("Continuous contribution at the centre (°C/s). Negative = cooling.")]
    public float strengthCPerSec = 20f;

    [Tooltip("World radius of influence (units).")]
    public float radius = 20f;

    [Tooltip("1 at centre -> 0 at edge.")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Require line-of-sight for full effect.")]
    public bool useLineOfSight = true;

    public LayerMask losBlockers = 0;

    [Header("Debug Settings")]
    public bool showDebugVisualizer;
    public int segments = 64;
    public float lineWidth = 0.03f;
    public Color colour = new(1f, 0.45f, 0f, 0.9f);

    private SphereCollider _triggerCollider;

    private LineRenderer _ringXY, _ringXZ, _ringYZ;
    private static Material _lineMaterial;
    
    private void OnValidate()
    {
        if (radius <= 0)
        {
            Debug.LogWarning("Radius must be greater than zero.");
            radius = 0.01f;
        }
        
        if (!_triggerCollider) _triggerCollider = GetComponent<SphereCollider>();
        if (_triggerCollider) _triggerCollider.radius = radius;
    }
    
    private void Awake()
    {
        showDebugVisualizer = true;
        
        if (!_triggerCollider) _triggerCollider = GetComponent<SphereCollider>();
        if (!_triggerCollider) _triggerCollider = gameObject.AddComponent<SphereCollider>();
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = radius;

        if (!_lineMaterial)
        {
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        if (losBlockers.value == 0)
        {
            losBlockers = LayerMask.GetMask("Water", "Room", "Terrain", "Vehicle");
        }
    }

    private void OnEnable()
    {
        if (_triggerCollider)
        {
            _triggerCollider.enabled = true;
        }
        
        EnsureWire(showDebugVisualizer);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        if (_triggerCollider)
        {
            _triggerCollider.enabled = true;
        }
        
        EnsureWire(false);
    }

    private void LateUpdate()
    {
        if (showDebugVisualizer) UpdateWire();
    }

    public override float GetHeatRateAt(Vector3 targetPos)
    {
        float distance = Vector3.Distance(transform.position, targetPos);
        if (distance >= radius)
            return 0f;

        // todo: maybe introduce an "accuracy" parameter which controlls the hertz/time between raycasts of the LOS raycast.
        // It would make it so a raycast could only be done maybe once every half a second, instead of every single time the GetHeatRateAt function is called,
        // the same could be done for other heat emitter stuff maybe
        float los = 1f;
        if (useLineOfSight && Physics.Raycast(
                transform.position, (targetPos - transform.position).normalized,
                distance, losBlockers, QueryTriggerInteraction.Ignore))
        {
            los = 0.15f;
        }

        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float attenuation = falloff.Evaluate(normalizedDistance);
        return strengthCPerSec * attenuation * los;
    }

    #region Debug Wireframe
    private void EnsureWire(bool enable)
    {
        if (enable)
        {
            if (!_ringXY) _ringXY = CreateRing("RingXY");
            if (!_ringXZ) _ringXZ = CreateRing("RingXZ");
            if (!_ringYZ) _ringYZ = CreateRing("RingYZ");
            UpdateWire();
        }
        else
        {
            DestroyRing(ref _ringXY);
            DestroyRing(ref _ringXZ);
            DestroyRing(ref _ringYZ);
        }
    }

    private LineRenderer CreateRing(string gameObjectName)
    {
        GameObject go = new(gameObjectName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.material = _lineMaterial;
        lr.textureMode = LineTextureMode.Stretch;
        lr.positionCount = Mathf.Max(8, segments);
        lr.startWidth = lr.endWidth = lineWidth;
        lr.startColor = lr.endColor = colour;
        return lr;
    }

    private void DestroyRing(ref LineRenderer lr)
    {
        if (!lr) return;
        Destroy(lr.gameObject);
        lr = null;
    }

    private void UpdateWire()
    {
        if (!_ringXY || !_ringXZ || !_ringYZ) return;

        int n = Mathf.Max(8, segments);
        float step = Mathf.PI * 2f / n;

        // XY plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            _ringXY.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        
        // XZ plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            _ringXZ.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        
        // YZ plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            _ringYZ.SetPosition(i, new Vector3(0f, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
        }

        // Keep colors/widths synced if changed at runtime
        for (int i = 0; i < new[] { _ringXY, _ringXZ, _ringYZ }.Length; i++)
        {
            LineRenderer lr = new[] { _ringXY, _ringXZ, _ringYZ }[i];
            lr.startWidth = lr.endWidth = lineWidth;
            lr.startColor = lr.endColor = colour;
        }
    }
    #endregion
}