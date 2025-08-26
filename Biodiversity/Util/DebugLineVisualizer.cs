using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Util;

public static class DebugLineVisualizer
{
    private static Material _lineMaterial;

    // A pool to reuse LineRenderer GameObjects instead of creating/destroying them constantly.
    private static readonly List<LineRenderer> _linePool = [];
    private static int _poolIndex;
    
    private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
    private static readonly int Cull = Shader.PropertyToID("_Cull");

    private static void CreateLineMaterial()
    {
        if (_lineMaterial) return;
        
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        
        // Set properties for transparency.
        _lineMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt(ZWrite, 0);
        _lineMaterial.SetInt(Cull, (int)UnityEngine.Rendering.CullMode.Off);
    }

    // Call this at the start of your debug update to reset the pool.
    public static void Reset()
    {
        _poolIndex = 0;
        // Hide any lines that were used last frame but not this frame.
        for (int i = _poolIndex; i < _linePool.Count; i++)
        {
            _linePool[i].gameObject.SetActive(false);
        }
    }

    public static void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        LineRenderer line = GetNextLineFromPool();
        line.gameObject.SetActive(true);

        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private static LineRenderer GetNextLineFromPool()
    {
        if (_poolIndex < _linePool.Count)
        {
            // Reuse an existing LineRenderer from the pool.
            return _linePool[_poolIndex++];
        }

        // Pool is empty, create a new one.
        CreateLineMaterial(); // Ensure material exists.

        GameObject lineObj = new("DebugLine");
        lineObj.transform.SetParent(null);
        lineObj.hideFlags = HideFlags.HideAndDontSave;
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.material = _lineMaterial;
        line.positionCount = 2;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        
        _linePool.Add(line);
        _poolIndex++;
        
        return line;
    }
}