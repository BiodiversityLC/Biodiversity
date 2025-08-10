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
    [Header("Environment")]
    public float ambientC = 20f;
    public float coolingTimeConstant = 6f;
    
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
        if (!TryGetComponent(out Rigidbody rb)) {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; 
            rb.useGravity = false;
        }

        TemperatureC = ambientC;
    }

    private void OnEnable()
    {
        HeatController.Instance.OnHeatEmitterDisabled += OnHeatEmitterDisabled;
    }

    private void OnDisable()
    {
        HeatController.Instance.OnHeatEmitterDisabled -= OnHeatEmitterDisabled;
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
        var bob = HeatController.Instance;
    }

    private void Update()
    {
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

    public void OnHeatEmitterDisabled(HeatEmitter emitter)
    {
        overlaps.Remove(emitter);
    }
}