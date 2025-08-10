using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Biodiversity.Behaviours.Heat;

public class HeatController
{
    private static readonly object _padlock = new();
    private static HeatController _instance;

    public static HeatController Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (_padlock)
            {
                _instance ??= new HeatController();
                _instance.SetupHeatEmitters();
            }
            
            return _instance;
        }
    }
    
    public static bool HasInstance => _instance != null;

    private readonly List<Func<int>> _heatEmitterSetups = [];
    
    public Action<HeatEmitter> OnHeatEmitterDisabled;

    private HeatController()
    {
        _heatEmitterSetups.Add(() =>
        {
            IEnumerable<Transform> fireplaces = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .Where(t => t.name == "FireplaceFire");

            int configured = 0;

            foreach (Transform fireplace in fireplaces)
            {
                Transform lightTransform = fireplace.Find("Light")
                                           ?? fireplace.GetComponentsInChildren<Transform>(true)
                                               .FirstOrDefault(t => t.name == "Light");

                if (!lightTransform)
                {
                    BiodiversityPlugin.LogVerbose($"[FireplaceHeatEmitterSetup] 'FireplaceFire' has no child/descendant 'Light'.");
                    continue;
                }
                
                Light light = lightTransform.GetComponent<Light>();
                if (!light)
                {
                    BiodiversityPlugin.LogVerbose($"[FireplaceHeatEmitterSetup] 'Light' under 'FireplaceFire' has no Light component.");
                    continue;
                }

                if (light.type == LightType.Directional)
                {
                    BiodiversityPlugin.LogVerbose($"[FireplaceHeatEmitterSetup] 'Light' is Directional, hence the radius of the light can't be used as the heat emitter radius. The default radius will be used.");
                }
                
                RadialHeatEmitter emitter = lightTransform.GetComponent<RadialHeatEmitter>() 
                                            ?? lightTransform.gameObject.AddComponent<RadialHeatEmitter>();
                emitter.radius = light.range;
                
                configured++;
            }

            return configured;
        });
    }

    private void SetupHeatEmitters()
    {
        int total = 0;
        for (int i = 0; i < _heatEmitterSetups.Count; i++)
        {
            Func<int> setup = _heatEmitterSetups[i];
            try
            {
                total += setup?.Invoke() ?? 0;
            }
            catch (Exception e)
            {
                BiodiversityPlugin.Logger.LogError($"[HeatController] Error occured while running a heat emitter setup function: {e}");
            }
        }
        
        BiodiversityPlugin.LogVerbose($"[HeatController] {total} heat emitters setup.");
        // Finds all of the objects that should be given a heat emitter component.
        // If more objects spawn later on in the game, a harmony patch attached to their main component will make it give itself a heat emitter component.
    }
}