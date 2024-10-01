using UnityEngine;

namespace Biodiversity.Creatures.Critters.LeafBoy.Formations;

internal interface IFormation
{
    FormationType FormationType { get; }
    
    int MinimumLeafBoysNeeded { get; }
    
    float MinimumHorizontalSpaceNeeded { get; }
    
    /// <summary>
    /// Calculates the target position for a follower based on its index in the formation.
    /// </summary>
    /// <param name="leaderPosition">The current position of the leader.</param>
    /// <param name="followerIndex">The index of the follower in the formation.</param>
    /// <param name="leaderSize">The size of the leader to determine spacing.</param>
    /// <param name="totalFollowers">Total number of followers in the group.</param>
    /// <returns>The calculated target position for the follower.</returns>
    Vector3 GetFollowerTargetPosition(Vector3 leaderPosition, int followerIndex, float leaderSize, int totalFollowers);
}

internal enum FormationType
{
    Line,
    Triangle,
    Prototax
}