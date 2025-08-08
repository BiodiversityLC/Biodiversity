using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

public abstract class HeatEmitter : MonoBehaviour
{
    private Rigidbody rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// Return contribution in °C/s for a target world position NOW.
    /// </summary>
    /// <param name="targetPos">The target world position.</param>
    /// <returns>Contribution in °C/s,</returns>
    public abstract float GetHeatRateAt(Vector3 targetPos);
}