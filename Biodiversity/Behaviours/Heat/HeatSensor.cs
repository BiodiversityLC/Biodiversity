using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

[RequireComponent(typeof(Collider))]
public class HeatSensor : NetworkBehaviour
{
    [Tooltip("How many times the heat rate is calculated per second.")]
    public float updateHz = 10f;
    
    [Tooltip("Whether to only compute the heat rate on the server (host) instance only.")]
    [SerializeField] private bool serverOnly = true;
    
    public float heatRate { get; private set; }
    
    private readonly HashSet<HeatEmitter> overlaps = [];
    
    private float timeSinceLastUpdate;

    private void OnValidate()
    {
        if (updateHz < 0f)
        {
            Debug.LogError("Update hertz must be greater than or equal to zero.");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (serverOnly && !IsServer)
        {
            enabled = false;
            overlaps.Clear();
            return;
        }
    }

    private void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, updateHz);
        if (timeSinceLastUpdate < step) return;
        timeSinceLastUpdate = 0f;

        heatRate = 0f;
        Vector3 position = transform.position;
        foreach (HeatEmitter emitter in overlaps)
        {
            if (emitter && emitter.isActiveAndEnabled)
            {
                heatRate += emitter.GetHeatRateAt(position);
            }
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
}