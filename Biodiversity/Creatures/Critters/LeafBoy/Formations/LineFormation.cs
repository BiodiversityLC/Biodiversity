using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.LeafBoy.Formations;

public class LineFormation : IFormation
{
    public FormationType FormationType => FormationType.Line;
    public int MinimumLeafBoysNeeded => 1;
    public float MinimumHorizontalSpaceNeeded => 1;

    public Vector3 GetFollowerTargetPosition(List<Vector3> leaderPathHistory, int followerIndex, float leaderSize)
    {
        if (leaderPathHistory.Count == 0)
        {
            return Vector3.zero;
        }

        int pathHistoryMinusOne = leaderPathHistory.Count - 1;
        int targetIndex = Mathf.Clamp(pathHistoryMinusOne - followerIndex, 0, pathHistoryMinusOne);
        Vector3 historicalPosition = leaderPathHistory[targetIndex];

        float spacing = leaderSize * 1.25f;

        return historicalPosition + Vector3.back * spacing * (followerIndex + 1);
    }
}