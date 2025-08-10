using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

public abstract class HeatEmitter : MonoBehaviour
{
    private void OnDisable()
    {
        if (HeatController.HasInstance)
            HeatController.Instance.OnHeatEmitterDisabled?.Invoke(this);
    }

    /// <summary>
    /// Calculates the contribution in °C/s for a target world position NOW.
    /// </summary>
    /// <param name="targetPos">The target world position.</param>
    /// <returns>Contribution in °C/s.</returns>
    public abstract float GetHeatRateAt(Vector3 targetPos);
}