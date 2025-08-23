using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

public class HeatController : MonoBehaviour
{
    private static readonly object _padlock = new();
    private static bool _isQuitting;
    
    private static HeatController _instance;
    public static HeatController Instance
    {
        get
        {
            if (_isQuitting) return null;
            
            if (!_instance)
            {
                _instance = FindAnyObjectByType<HeatController>();

                lock (_padlock)
                {
                    if (!_instance)
                    {
                        GameObject singleton = new() { name = "BiodiverseHeatController" };
                        _instance = singleton.AddComponent<HeatController>();
                        BiodiversityPlugin.LogVerbose("[HeatController] Created new HeatController.");
                    }
                }
            }
            
            return _instance;
        }
    }
    
    public static bool HasInstance => _instance;
    private bool _hasDoneInitialSweep;

    // A simple registry of "rules": if predicate matches, run attach action
    private readonly List<(Func<GameObject, bool> match, Action<GameObject> attach)> _heatEmitterRules = [];
    
    private readonly HashSet<int> _seenGameObjects = [];
    
    public Action<HeatEmitter> OnHeatEmitterDisabled;
    
    public int SensorCount { get; private set; }

    private void Awake()
    {
        // Flashlight items
        _heatEmitterRules.Add((go => go.TryGetComponent(out FlashlightItem _), AttachFlashlight));
        
        // Fireplace in the vanilla mansion
        _heatEmitterRules.Add((
            go => go.name.IndexOf("FireplaceFire", StringComparison.OrdinalIgnoreCase) >= 0,
            AttachFireplace));
        
        _heatEmitterRules.Add((go => go.TryGetComponent(out LungProp _), AttachApparatus));
    }

    private void OnDestroy()
    {
        _isQuitting = true;
    }

    public void TryAttachEmitter(GameObject go)
    {
        if (SensorCount <= 0) return;
        if (!go || !go.activeInHierarchy) return;

        // Check if the gameobject already has an emitter
        if (go.TryGetComponent(out HeatEmitter _)) return;
        
        // Checks if we have already tried to attach an emitter to this object
        int id = go.GetInstanceID();
        if (_seenGameObjects.Contains(id)) return;

        for (int i = 0; i < _heatEmitterRules.Count; i++)
        {
            (Func<GameObject, bool> match, Action<GameObject> attach) = _heatEmitterRules[i];
            if (!match(go)) continue;

            try
            {
                attach(go);
                _seenGameObjects.Add(id);
            }
            catch (Exception e)
            {
                BiodiversityPlugin.LogVerbose($"[HeatController] Failed to attach heat emitter to '{go.name}'. {e}");
            }
        }
    }

    private IEnumerator InitialSceneSweep()
    {
        _hasDoneInitialSweep = true;

        const float maxFrameTimeMilliseconds = 2f;
        Stopwatch stopwatch = new();
        
        BiodiversityPlugin.LogVerbose($"[HeatController] Starting initial scene sweep.");
        
        Transform[] trs = FindObjectsOfType<Transform>(true);
        
        stopwatch.Start();
        for (int i = 0; i < trs.Length; i++)
        {
            Transform tr = trs[i];
            if (tr && tr.gameObject.activeInHierarchy)
            {
                TryAttachEmitter(tr.gameObject);
            }

            if (stopwatch.Elapsed.TotalMilliseconds > maxFrameTimeMilliseconds)
            {
                yield return null;
                stopwatch.Restart();
            }
        }
        
        BiodiversityPlugin.LogVerbose($"[HeatController] Initial scene sweep complete.");
    }

    public void NotifySensorAdded()
    {
        SensorCount++;
        if (!_hasDoneInitialSweep) StartCoroutine(InitialSceneSweep());
    }

    public void NotifySensorRemoved()
    {
        SensorCount = Mathf.Max(0, SensorCount - 1);
    }

    private void AttachFlashlight(GameObject go)
    {
        if (!go.TryGetComponent(out FlashlightItem flashlightItem))
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The given gameobject has no {nameof(FlashlightItem)} component.");
            return;
        }

        Light bulb = flashlightItem.flashlightBulb;
        if (!bulb)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The {nameof(FlashlightItem)}'s flashlightBulb is null.");
            return;
        }

        if (bulb.type != LightType.Spot)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The flashlightBulb's light type is invalid ({bulb.type}), it must be a Spot.");
            return;
        }
        
        GameObject coneObject = new("HeatCone");
        coneObject.transform.SetParent(bulb.transform, false);
        coneObject.transform.localPosition = Vector3.zero;
        coneObject.transform.localRotation = Quaternion.identity;

        DirectedConeHeatEmitter cone = coneObject.AddComponent<DirectedConeHeatEmitter>();
        cone.range = bulb.range;
        cone.outerAngle = bulb.spotAngle;
        cone.innerAngle = Mathf.Clamp(bulb.innerSpotAngle, 0f, bulb.spotAngle);
        cone.segments = 24;
    }

    private void AttachFireplace(GameObject go)
    {
        Light fireplaceLight = go.GetComponentInChildren<Light>();
        if (!fireplaceLight)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFireplace)}] 'FireplaceFire' has no child Light component.");
            return;
        }

        RadialHeatEmitter emitter = fireplaceLight.GetComponent<RadialHeatEmitter>();
        if (!emitter)
        {
            emitter = fireplaceLight.gameObject.AddComponent<RadialHeatEmitter>();
        }
        
        if (fireplaceLight.type == LightType.Directional)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFireplace)}] 'Light' is Directional, hence the radius of the light can't be used as the heat emitter radius. The default radius will be used.");
        }
        else
        {
            emitter.radius = fireplaceLight.range;
        }
    }

    private void AttachApparatus(GameObject go)
    {
        if (!go.TryGetComponent(out LungProp apparatus))
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachApparatus)}] The given gameobject has no {nameof(LungProp)} component.");
            return;
        }
        
        RadialHeatEmitter emitter = apparatus.GetComponent<RadialHeatEmitter>();
        if (!emitter)
        {
            emitter = apparatus.gameObject.AddComponent<RadialHeatEmitter>();
        }
        
        // todo: make the heat emitter turn off/on when the `apparatus.isLungPowered` gets toggled
    }
}