using UnityEngine;

namespace Biodiversity.Util.DataStructures;

[System.Serializable]
public struct NetworkPositionalSyncFidelity
{
    [Tooltip("Controls how aggressively the client's model catches up to the server's actual position. A smaller value is faster and snappier, but can appear jittery on laggy connections.")]
    public float InterpolationAggressiveness;

    [Tooltip("The distance the enemy must move from its last synced position before the server sends a new network update. A smaller value means more frequent, accurate updates at the cost of higher bandwidth.")]
    public float UpdateDistanceThreshold;
}