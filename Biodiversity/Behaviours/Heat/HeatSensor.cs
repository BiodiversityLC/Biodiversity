using Biodiversity.Util;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[RequireComponent(typeof(Collider))]
public class HeatSensor : NetworkBehaviour
{
    [Tooltip("How many times the heat rate is calculated per second.")]
    public float updateHz = 20f;
    
    [Space(2f)]
    [Header("Environment Settings")]
    public float ambientC = 20f;
    public float coolingTimeConstant = 6f;

    [Space(2f)]
    [Header("Debug Settings")]
    public bool debug = true;
    
    public float TemperatureC { get; private set; }
    
    private readonly HashSet<HeatEmitter> overlaps = [];
    
    private float timeSinceLastUpdate;
    private float impulseBufferC;

    private void OnValidate()
    {
        if (updateHz < 0f)
        {
            Debug.LogError("Update hertz must be greater than or equal to zero.");
        }
    }

    private void Awake()
    {
        if (!TryGetComponent(out Rigidbody rb)) 
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; 
            rb.useGravity = false;
        }

        TemperatureC = ambientC;
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
        Vector3 position = transform.position;
        foreach (HeatEmitter emitter in overlaps)
        {
            if (emitter && emitter.isActiveAndEnabled)
            {
                float additionalHeatRate = emitter.GetHeatRateAt(position);
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
    
    private void DrawRuntimeDebug()
    {
        if (!debug) return;

        // Every frame, reset the visualizer so old lines disappear.
        DebugLineVisualizer.Reset();

        // Create a color gradient (same as the Gizmo version).
        Gradient heatGradient = new();
        heatGradient.SetKeys(
            [new GradientColorKey(Color.blue, 0.0f), new GradientColorKey(Color.yellow, 0.5f), new GradientColorKey(Color.red, 1.0f)],
            [new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.8f, 1.0f)] // 80% alpha
        );
    
        Vector3 position = transform.position;

        // Use a temporary list to avoid issues if the overlaps set is modified.
        List<HeatEmitter> emittersToRemove = new()
        {
            Capacity = 0
        };

        foreach (HeatEmitter emitter in overlaps)
        {
            if (!emitter || !emitter.isActiveAndEnabled)
            {
                emittersToRemove.Add(emitter);
                continue;
            }

            float heatRate = emitter.GetHeatRateAt(position);
        
            // Normalize the heat rate for the gradient color.
            float colorT = Mathf.InverseLerp(0, 30, heatRate);
            Color lineColor = heatGradient.Evaluate(colorT);

            // Use our new tool to draw a line in the game world.
            DebugLineVisualizer.DrawLine(position, emitter.transform.position, lineColor);
        }
    
        // Cleanup any dead emitters
        for (int i = 0; i < emittersToRemove.Count; i++)
        {
            HeatEmitter emitter = emittersToRemove[i];
            overlaps.Remove(emitter);
        }
    }
}