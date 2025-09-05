using Biodiversity.Util;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[RequireComponent(typeof(Collider))]
public class HeatSensor : NetworkBehaviour
{
    [Header("Heat Settings")]
    [Tooltip("How many times the heat rate is calculated per second.")]
    public float updateHz = 10f;

    public float ambientC = 20f;
    public float coolingTimeConstant = 6f;

    [Space(2f)]
    [Header("Debug Settings")]
    public bool debug = true;

    [Space(2f)]
    [Header("References")]
    [SerializeField] private Collider sensorCollider;

    public float TemperatureC { get; private set; }

    private readonly HashSet<HeatEmitter> overlaps = [];

    private Gradient _debugHeatGradient;
    private readonly List<HeatEmitter> _emittersToRemoveCache = [];

    private float timeSinceLastUpdate;
    private float impulseBufferC;

    private void OnValidate()
    {
        if (updateHz < 0f)
        {
            Debug.LogError("Update hertz must be greater than or equal to zero.");
        }
    }

    private void OnEnable()
    {
        HeatController.Instance.NotifySensorAdded();
        HeatController.Instance.OnHeatEmitterDisabled += HandleHeatEmitterDisabled;
    }

    private void OnDisable()
    {
        HeatController.Instance.NotifySensorRemoved();
        HeatController.Instance.OnHeatEmitterDisabled -= HandleHeatEmitterDisabled;

        DebugShapeVisualizer.Clear(this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
            overlaps.Clear();
            return;
        }
    }

    private void Start()
    {
        if (!sensorCollider)
        {
            sensorCollider = GetComponent<Collider>();
            if (!sensorCollider)
            {
                BiodiversityPlugin.Logger.LogWarning($"Sensor collider not found for object {gameObject.name}. Disabiling the component...");
                enabled = false;
                return;
            }
        }

        if (!TryGetComponent(out Rigidbody rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        TemperatureC = ambientC;

        if (_debugHeatGradient == null)
        {
            _debugHeatGradient = new Gradient();
            _debugHeatGradient.SetKeys(
                [new GradientColorKey(Color.blue, 0.0f), new GradientColorKey(Color.yellow, 0.5f), new GradientColorKey(Color.red, 1.0f)],
                [new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.8f, 1.0f)]
            );
        }
    }

    private void Update()
    {
        DrawRuntimeDebug();

        timeSinceLastUpdate += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, updateHz);

        while (timeSinceLastUpdate >= step)
        {
            Integrate(step);
            timeSinceLastUpdate -= step;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out HeatEmitter emitter)) overlaps.Add(emitter);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out HeatEmitter emitter)) overlaps.Remove(emitter);
    }

    private void Integrate(float dt)
    {
        float rateSum = 0f;
        foreach (HeatEmitter emitter in overlaps)
        {
            if (emitter && emitter.isActiveAndEnabled)
            {
                Vector3 samplePoint = sensorCollider.ClosestPoint(emitter.transform.position);

                float additionalHeatRate = emitter.GetHeatRateAt(samplePoint);
                rateSum += additionalHeatRate;
            }
        }

        float e = Mathf.Exp(-dt / Mathf.Max(0.0001f, coolingTimeConstant));
        TemperatureC = ambientC
                       + (TemperatureC - ambientC) * e
                       + (1f - e) * (coolingTimeConstant * rateSum)
                       + impulseBufferC;

        impulseBufferC = 0f;
    }

    public void AddHeatImpulse(float deltaC) => impulseBufferC += deltaC;

    public void HandleHeatEmitterDisabled(HeatEmitter emitter)
    {
        overlaps.Remove(emitter);
    }

    #region Debug
    private void DrawRuntimeDebug()
    {
        if (!debug) return;

        // Every frame, reset the visualizer so old lines disappear
        DebugShapeVisualizer.Clear(this);

        Vector3 position = transform.position + Vector3.up * 0.5f;
        _emittersToRemoveCache.Clear();

        foreach (HeatEmitter emitter in overlaps)
        {
            if (!emitter || !emitter.isActiveAndEnabled)
            {
                _emittersToRemoveCache.Add(emitter);
                continue;
            }

            Vector3 samplePoint = sensorCollider.ClosestPoint(emitter.transform.position);
            float heatRate = emitter.GetHeatRateAt(samplePoint);

            // Normalize the heat rate for the gradient color
            float colorT = Mathf.InverseLerp(0, 30, heatRate);
            Color lineColor = _debugHeatGradient.Evaluate(colorT);

            DebugShapeVisualizer.DrawLine(this, samplePoint, emitter.transform.position, lineColor);
        }

        for (int i = 0; i < _emittersToRemoveCache.Count; i++)
        {
            HeatEmitter emitter = _emittersToRemoveCache[i];
            overlaps.Remove(emitter);
        }
    }
    #endregion
}