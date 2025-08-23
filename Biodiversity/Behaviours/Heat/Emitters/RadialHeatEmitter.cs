using Biodiversity.Creatures.WaxSoldier;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[DisallowMultipleComponent]
public class RadialHeatEmitter : HeatEmitter
{
    [Header("Heat Settings")]
    [Tooltip("Continuous contribution at the centre (°C/s). Negative = cooling.")]
    public float strengthCPerSec = 20f;

    [Tooltip("World radius of influence (units).")]
    public float radius = 10f;

    [Tooltip("1 at centre -> 0 at edge.")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Require line-of-sight for full effect.")]
    public bool useLineOfSight = true;

    public LayerMask losBlockers = ~0;

    [Header("Debug Wireframe")]
    public bool showDebugWireframe;
    public int segments = 64;
    public float lineWidth = 0.03f;
    public Color colour = new(1f, 0.45f, 0f, 0.9f);

    private SphereCollider triggerCollider;

    private LineRenderer ringXY, ringXZ, ringYZ;
    private static Material lineMaterial;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (radius <= 0)
        {
            Debug.LogWarning("Radius must be greater than zero.");
            radius = 0.01f;
        }
        
        if (!triggerCollider) triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider) triggerCollider.radius = radius;

        if (showDebugWireframe && Application.isPlaying)
        {
            UpdateWire();
        }
    }
#endif
    
    private void Awake()
    {
        if (!triggerCollider) triggerCollider = GetComponent<SphereCollider>();
        if (!triggerCollider) triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = radius;

        if (!lineMaterial)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        if (losBlockers.value == 0)
        {
            losBlockers = LayerMask.GetMask("Water", "Room", "Terrain", "Vehicle");
        }

        #if !UNITY_EDITOR
        showDebugWireframe = WaxSoldierHandler.Instance.Config.EnableDebugWireframeForRadialHeatEmitters;
        #endif
    }

    private void OnEnable()
    {
        if (triggerCollider)
        {
            triggerCollider.enabled = true;
        }
        
        EnsureWire(showDebugWireframe);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        
        if (triggerCollider)
        {
            triggerCollider.enabled = true;
        }
        
        EnsureWire(false);
    }

    private void LateUpdate()
    {
        if (showDebugWireframe) UpdateWire();
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
            if (!ringXY) ringXY = CreateRing("RingXY");
            if (!ringXZ) ringXZ = CreateRing("RingXZ");
            if (!ringYZ) ringYZ = CreateRing("RingYZ");
            UpdateWire();
        }
        else
        {
            DestroyRing(ref ringXY);
            DestroyRing(ref ringXZ);
            DestroyRing(ref ringYZ);
        }
    }

    private LineRenderer CreateRing(string gameObjectName)
    {
        GameObject go = new(gameObjectName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.material = lineMaterial;
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
        if (!ringXY || !ringXZ || !ringYZ) return;

        int n = Mathf.Max(8, segments);
        float step = Mathf.PI * 2f / n;

        // XY plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            ringXY.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
        
        // XZ plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            ringXZ.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        
        // YZ plane
        for (int i = 0; i < n; i++)
        {
            float a = i * step;
            ringYZ.SetPosition(i, new Vector3(0f, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
        }

        // Keep colors/widths synced if changed at runtime
        for (int i = 0; i < new[] { ringXY, ringXZ, ringYZ }.Length; i++)
        {
            LineRenderer lr = new[] { ringXY, ringXZ, ringYZ }[i];
            lr.startWidth = lr.endWidth = lineWidth;
            lr.startColor = lr.endColor = colour;
        }
    }
    #endregion
}