using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public static class AloeUtils
{
    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "OutsideAINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all outside AI nodes.</returns>
    public static IEnumerable<Vector3> FindOutsideAINodePositions()
    {
        GameObject[] outsideAINodes = AloeSharedData.Instance.GetOutsideAINodes();
        Vector3[] outsideNodePositions = new Vector3[outsideAINodes.Length];
                
        for (int i = 0; i < outsideAINodes.Length; i++)
        {
            outsideNodePositions[i] = outsideAINodes[i].transform.position;
        }
        
        return outsideNodePositions;
    }

    /// <summary>
    /// Finds and returns the positions of all AI nodes tagged as "AINode".
    /// </summary>
    /// <returns>An enumerable collection of Vector3 positions for all inside AI nodes.</returns>
    public static IEnumerable<Vector3> FindInsideAINodePositions()
    {
        GameObject[] insideAINodes = AloeSharedData.Instance.GetInsideAINodes();
        Vector3[] insideNodePositions = new Vector3[insideAINodes.Length];
                
        for (int i = 0; i < insideAINodes.Length; i++)
        {
            insideNodePositions[i] = insideAINodes[i].transform.position;
        }
        
        return insideNodePositions;
    }
}