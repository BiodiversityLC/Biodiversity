using System.Collections.Generic;
using UnityEngine;

namespace Biodiversity.Creatures.Critters.LeafBoy.Formations;

//todo: look into static abstract/parent classes
internal interface IFormation
{
    FormationType FormationType { get; }
    
    int MinimumLeafBoysNeeded { get; }
    
    float MinimumHorizontalSpaceNeeded { get; } //todo: this is fucked
    
    Vector3 GetFollowerTargetPosition(List<Vector3> leaderPathHistory, int followerIndex, float leaderSize);
}

public enum FormationType
{
    Line,
    Triangle,
    Prototax
}