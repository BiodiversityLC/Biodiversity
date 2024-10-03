using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.LeafBoy.Formations;

public class TriangleFormation : IFormation
{
    public FormationType FormationType => FormationType.Triangle;
    public int MinimumLeafBoysNeeded => 3;
    public float MinimumHorizontalSpaceNeeded => 3;
    
    public Vector3 GetFollowerTargetPosition(List<Vector3> leaderPathHistory, int followerIndex, float leaderSize)
    {
        if (leaderPathHistory.Count == 0)
        {
            return Vector3.zero;
        }
        
        int pathHistoryMinusOne = leaderPathHistory.Count - 1;
        int targetIndex = Mathf.Clamp(pathHistoryMinusOne - followerIndex, 0, pathHistoryMinusOne);
        Vector3 historicalPosition = leaderPathHistory[targetIndex];
        
        float spacing = leaderSize;

        int row = followerIndex / 2;
        float lateralOffset = (followerIndex % 2 * 2 - 1) * (leaderSize / 2);

        return historicalPosition + Vector3.back * spacing * row + Vector3.right * lateralOffset;
    }
}

