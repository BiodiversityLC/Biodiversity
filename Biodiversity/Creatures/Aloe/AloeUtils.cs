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
    
    /// <summary>
    /// Smoothly moves the specified transform position to the target position.
    /// </summary>
    /// <param name="transform">The transform to move.</param>
    /// <param name="targetPosition">The target position to move to.</param>
    /// <param name="smoothTime">The time it takes to smooth to the target position.</param>
    /// <param name="velocity">A reference to the current velocity, this is modified by the function.</param>
    public static void SmoothMoveTransformPositionTo(Transform transform, Vector3 targetPosition, float smoothTime, ref Vector3 velocity)
    {
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}