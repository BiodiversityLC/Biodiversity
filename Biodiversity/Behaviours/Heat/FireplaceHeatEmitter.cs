using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

public class FireplaceHeatEmitter : HeatEmitter
{
    [Header("Heat Settings")]
    [Tooltip("Continuous contribution at the center (°C/s). Negative = cooling.")]
    public float strengthCPerSec = 20f;

    [Tooltip("World radius of influence (units).")]
    public float radius = 7f;

    [Tooltip("1 at centre -> 0 at edge.")]
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Require line-of-sight for full effect.")]
    public bool useLineOfSight = true;

    public LayerMask losBlockers = ~0;

    private SphereCollider triggerCollider;

    private void OnValidate()
    {
        if (radius <= 0)
        {
            Debug.LogError("Radius must be greater than zero.");
        }
    }
    
    protected override void Awake()
    {
        base.Awake();
        
        triggerCollider = GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = radius;
    }

    public override float GetHeatRateAt(Vector3 targetPos)
    {
        float distance = Vector3.Distance(transform.position, targetPos);
        if (distance >= radius)
            return 0f;

        float los = 1f;
        if (useLineOfSight && Physics.Raycast(
                transform.position, (targetPos - transform.position).normalized,
                distance, losBlockers, QueryTriggerInteraction.Ignore))
        {
            los = 0.15f;
        }

        float normalizedDistance = Mathf.Clamp01(distance / radius);
        float attenuation = falloff.Evaluate(1f - normalizedDistance);
        return strengthCPerSec * attenuation * los;
    }
}