using System;
using System.Collections;
using System.Collections.Generic;
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
                        GameObject singleton = new() { name = "HeatController" };
                        _instance = singleton.AddComponent<HeatController>();
                        BiodiversityPlugin.LogVerbose("Created new HeatController.");
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
        // Flashlight Items
        _heatEmitterRules.Add((go => go.TryGetComponent(out FlashlightItem _), AttachFlashlight));
        
        // Fireplace in the vanilla mansion
        _heatEmitterRules.Add((
            go => go.name.IndexOf("FireplaceFire", StringComparison.OrdinalIgnoreCase) >= 0,
            AttachFireplace));
        
        // todo: add apparatus heat emitter with similar settings to fireplace (radial heat emitter)
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
                BiodiversityPlugin.LogVerbose($"Failed to attach heat emitter to '{go.name}'. {e}");
            }
        }
    }

    private IEnumerator InitialSceneSweep()
    {
        _hasDoneInitialSweep = true;
        
        Transform[] trs = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            Transform tr = trs[i];
            if (!tr) continue;
            
            GameObject go = tr.gameObject;
            if (!go.activeInHierarchy) continue;
            
            TryAttachEmitter(tr.gameObject);
            yield return null;
        }
        
        BiodiversityPlugin.LogVerbose($"[HeatController] Initial scene sweep complete.");
        // Finds all of the objects that should be given a heat emitter component.
        // If more objects spawn later on in the game, a harmony patch attached to their main component will make it give itself a heat emitter component.
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
            BiodiversityPlugin.LogVerbose($"[{nameof(AttachFlashlight)}] The given gameobject has no FlashlightItem component.");
            return;
        }

        Light bulb = flashlightItem.flashlightBulb;
        if (!bulb)
        {
            BiodiversityPlugin.LogVerbose($"[{nameof(AttachFlashlight)}] The FlashlightItem's flashlightBulb is null.");
            return;
        }

        if (bulb.type != LightType.Spot)
        {
            BiodiversityPlugin.LogVerbose($"[{nameof(AttachFlashlight)}] The flashlightBulb's light type is invalid ({bulb.type}), it must be a Spot.");
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
            BiodiversityPlugin.LogVerbose($"[{nameof(AttachFireplace)}] 'FireplaceFire' has no child Light component.");
            return;
        }
        
        if (fireplaceLight.type == LightType.Directional)
        {
            BiodiversityPlugin.LogVerbose($"[{nameof(AttachFireplace)}] 'Light' is Directional, hence the radius of the light can't be used as the heat emitter radius. The default radius will be used.");
        }
                
        RadialHeatEmitter emitter = fireplaceLight.GetComponent<RadialHeatEmitter>() 
                                    ?? fireplaceLight.gameObject.AddComponent<RadialHeatEmitter>();
        emitter.radius = fireplaceLight.range;
                
        //configured++;
    }
}