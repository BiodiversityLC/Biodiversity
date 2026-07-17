using Unity.Netcode;
using UnityEngine;

public class FloatThresholdNetworkVariable : NetworkVariable<float>
{
    public readonly float Threshold;
    private float _lastSentValue;

    // The constructor passes the initial value to the base NetworkVariable
    public FloatThresholdNetworkVariable(float initialValue = 0f, float threshold = 1f)
        : base(initialValue)
    {
        Threshold = threshold;
        _lastSentValue = initialValue;
    }

    /// <summary>
    /// Evaluates the new value against the threshold.
    /// If the difference is large enough, it updates the underlying NetworkVariable and triggers a sync.
    /// </summary>
    public void SetWithThreshold(float newValue)
    {
        if (Mathf.Abs(newValue - _lastSentValue) >= Threshold)
        {
            Value = newValue;
            _lastSentValue = newValue;
        }
    }
}