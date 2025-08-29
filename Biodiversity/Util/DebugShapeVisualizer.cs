using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Util;

public static class DebugShapeVisualizer
{
    private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
    private static readonly int Cull = Shader.PropertyToID("_Cull");
    
    private static Material _debugMaterial;
    
    private static readonly List<LineRenderer> _linePool = [];
    private static readonly List<Transform> _spherePool = [];
    
    // Dictionary to track which visual objects are owned by which client
    // Key: The client's instance ID (from GetHashCode())
    // Value: A list of the GameObjects (lines/spheres) that client is currently using
    private static readonly Dictionary<int, List<GameObject>> _activeVisuals = new();

    private static void CreateMaterial()
    {
        if (_debugMaterial) return;
        
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        _debugMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        
        // Set properties for transparency
        _debugMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _debugMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _debugMaterial.SetInt(ZWrite, 0);
        _debugMaterial.SetInt(Cull, (int)UnityEngine.Rendering.CullMode.Off);
        _debugMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
    
    /// <summary>
    /// Hides all visuals created by a specific owner.
    /// </summary>
    /// <param name="owner">The instance of the object that created the visuals (e.g., 'this').</param>
    public static void Clear(object owner)
    {
        int ownerId = owner.GetHashCode();
        if (_activeVisuals.TryGetValue(ownerId, out List<GameObject> visuals))
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                GameObject visual = visuals[i];
                visual.SetActive(false);
            }

            visuals.Clear(); // Clear the list for the next use
        }
    }

    public static void DrawLine(object owner, Vector3 start, Vector3 end, Color colour)
    {
        LineRenderer line = GetNextLine();
        line.gameObject.SetActive(true);
        
        line.startColor = colour;
        line.endColor = colour;
        
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        
        RegisterVisual(owner, line.gameObject);
    }

    public static void DrawSphere(object owner, Vector3 position, float radius, Color colour)
    {
        Transform sphere = GetNextSphere();
        sphere.gameObject.SetActive(true);
        
        sphere.position = position;
        sphere.localScale = Vector3.one * (radius * 2);
        
        // The GetNextSphere function ensures that this gameobject has a MeshRenderer
        sphere.GetComponent<MeshRenderer>().material.color = colour;
        
        RegisterVisual(owner, sphere.gameObject);
    }

    private static void RegisterVisual(object owner, GameObject visualObject)
    {
        int ownerId = owner.GetHashCode();
        if (!_activeVisuals.TryGetValue(ownerId, out List<GameObject> visuals))
        {
            visuals = [];
            _activeVisuals[ownerId] = visuals;
        }
        
        visuals.Add(visualObject);
    }

    private static LineRenderer GetNextLine()
    {
        for (int i = 0; i < _linePool.Count; i++)
        {
            LineRenderer line = _linePool[i];
            if (!line.gameObject.activeInHierarchy) return line;
        }

        // No inactive lines found, create a new line
        CreateMaterial();

        GameObject newLineObj = new("DebugLine") { hideFlags = HideFlags.HideAndDontSave };
        LineRenderer newLine = newLineObj.AddComponent<LineRenderer>();
        newLine.material = _debugMaterial;
        newLine.positionCount = 2;
        newLine.startWidth = 0.02f;
        newLine.endWidth = 0.02f;
        
        _linePool.Add(newLine);
        
        return newLine;
    }

    private static Transform GetNextSphere()
    {
        for (int i = 0; i < _spherePool.Count; i++)
        {
            Transform sphere = _spherePool[i];
            if (!sphere.gameObject.activeInHierarchy) return sphere;
        }

        // No inactive spheres found, create a new sphere
        CreateMaterial();
        
        GameObject newSphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        newSphereObj.name = "DebugSphere";
        newSphereObj.hideFlags = HideFlags.HideAndDontSave;

        if (newSphereObj.TryGetComponent(out Collider collider))
        {
            Object.Destroy(collider);
        }

        if (!newSphereObj.TryGetComponent(out MeshRenderer meshRenderer))
        {
            meshRenderer = newSphereObj.AddComponent<MeshRenderer>();
        }
        
        meshRenderer.material = _debugMaterial;

        Transform newSphere = newSphereObj.transform;
        _spherePool.Add(newSphere);
        
        return newSphere;
    }
}