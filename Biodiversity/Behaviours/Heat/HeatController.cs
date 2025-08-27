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
    private readonly List<(Func<GameObject, Component> match, Func<GameObject, HeatEmitter> attach)> _heatEmitterRules = [];
    
    private readonly Dictionary<Component, HeatEmitter> _componentToEmitterMap = new();
    private readonly Dictionary<HeatEmitter, Component> _emitterToComponentMap = new();
    
    private readonly HashSet<int> _seenGameObjects = [];
    
    public Action<HeatEmitter> OnHeatEmitterDisabled;
    
    public int SensorCount { get; private set; }

    private void Awake()
    {
        // Flashlight items
        _heatEmitterRules.Add((go => go.TryGetComponent(out FlashlightItem flashlight) ? flashlight : null, 
            AttachFlashlight));
        
        // Fireplace in the vanilla mansion
        _heatEmitterRules.Add((
            go => go.name.IndexOf("FireplaceFire", StringComparison.OrdinalIgnoreCase) >= 0 ? go.transform : null,
            AttachFireplace));
        
        _heatEmitterRules.Add((go => go.TryGetComponent(out LungProp lungProp) ? lungProp : null, 
            AttachApparatus));
    }
    
    private void OnEnable()
    {
        if (HasInstance)
            Instance.OnHeatEmitterDisabled += HandleHeatEmitterDisabled;
    }

    private void OnDisable()
    {
        if (HasInstance)
            Instance.OnHeatEmitterDisabled -= HandleHeatEmitterDisabled;
    }

    private void OnDestroy()
    {
        _isQuitting = true;
    }

    public void TryAttachEmitter(GameObject go)
    {
        if (SensorCount <= 0 || !go || !go.activeInHierarchy) return;

        // Check if the gameobject already has an emitter
        if (go.TryGetComponent(out HeatEmitter _)) return;
        
        // Checks if we have already tried to attach an emitter to this object
        int id = go.GetInstanceID();
        if (_seenGameObjects.Contains(id)) return;

        bool verboseEnabled = BiodiversityPlugin.Config.VerboseLoggingEnabled;
        for (int i = 0; i < _heatEmitterRules.Count; i++)
        {
            (Func<GameObject, Component> match, Func<GameObject, HeatEmitter> attach) = _heatEmitterRules[i];

            Component matchedComponent = match(go);
            if (!matchedComponent) continue;

            try
            {
                HeatEmitter createdEmitter = attach(go);
                if (createdEmitter)
                {
                    _componentToEmitterMap[matchedComponent] = createdEmitter;
                    _emitterToComponentMap[createdEmitter] = matchedComponent;
                    
                    // Just to make sure we dont use GetType() if verbose logging is not enabled
                    if (verboseEnabled)
                    {
                        BiodiversityPlugin.LogVerbose($"[HeatController] Mapped {matchedComponent.GetType().Name} to {createdEmitter.GetType().Name}.");
                    }
                }
                
                _seenGameObjects.Add(id);
                break;
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
    
    /// <summary>
    /// Retrieves the HeatEmitter associated with a specific component instance.
    /// </summary>
    /// <param name="componentKey">The original component instance (e.g., a FlashlightItem).</param>
    /// <returns>The associated HeatEmitter, or null if none exists.</returns>
    public HeatEmitter GetEmitterForComponent(Component componentKey)
    {
        _componentToEmitterMap.TryGetValue(componentKey, out HeatEmitter emitter);
        return emitter;
    }

    public void HandleHeatEmitterDisabled(HeatEmitter emitter)
    {
        if (_emitterToComponentMap.TryGetValue(emitter, out Component component))
        {
            _componentToEmitterMap.Remove(component);
            _emitterToComponentMap.Remove(emitter);
        }
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

    private DirectedConeHeatEmitter AttachFlashlight(GameObject go)
    {
        if (!go.TryGetComponent(out FlashlightItem flashlightItem))
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The given gameobject has no {nameof(FlashlightItem)} component.");
            return null;
        }

        Light bulb = flashlightItem.flashlightBulb;
        if (!bulb)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The {nameof(FlashlightItem)}'s flashlightBulb is null.");
            return null;
        }

        if (bulb.type != LightType.Spot)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFlashlight)}] The flashlightBulb's light type is invalid ({bulb.type}), it must be a Spot.");
            return null;
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

        switch (flashlightItem.itemProperties.itemName)
        {
            case "Pro-flashlight":
                cone.centreRateCPerSec = 1f;
                cone.range *= 2f;
                break;
            case "Flashlight":
                cone.centreRateCPerSec = 0.5f;
                cone.range *= 1.5f;
                break;
        }

        return cone;
    }

    private RadialHeatEmitter AttachFireplace(GameObject go)
    {
        Light fireplaceLight = go.GetComponentInChildren<Light>();
        if (!fireplaceLight)
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachFireplace)}] 'FireplaceFire' has no child Light component.");
            return null;
        }
        
        if (!fireplaceLight.TryGetComponent(out RadialHeatEmitter emitter))
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

        return emitter;
    }

    private RadialHeatEmitter AttachApparatus(GameObject go)
    {
        if (!go.TryGetComponent(out LungProp apparatus))
        {
            BiodiversityPlugin.LogVerbose($"[HeatController|{nameof(AttachApparatus)}] The given gameobject has no {nameof(LungProp)} component.");
            return null;
        }
        
        if (!apparatus.TryGetComponent(out RadialHeatEmitter emitter))
        {
            emitter = apparatus.gameObject.AddComponent<RadialHeatEmitter>();
        }
        
        emitter.enabled = apparatus.isLungPowered;
        emitter.radius = 20f;
        
        return emitter;
    }
}