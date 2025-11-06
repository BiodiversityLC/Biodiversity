using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Util
{
    public static class PositionUtils
    {
        public static Vector3 GetSeededMoonPosition(System.Random rand, bool insidePosition = false, float radius = 20f)
        {
            var nodes = insidePosition ? RoundManager.Instance.insideAINodes : RoundManager.Instance.outsideAINodes;
            var pos = nodes[rand.Next(0, nodes.Length)].transform.position;
            float y = pos.y;
            Quaternion quat = Quaternion.Euler((float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()));
            Vector3 forward = quat * Vector3.forward;
            pos = (forward * (float)(radius * rand.NextDouble())) + pos;
            pos.y = y;
            if (NavMesh.SamplePosition(pos, out var navHit, radius, -1))
            {
                return navHit.position;
            }
            return pos;
        }

        public static Vector3 GetRandomMoonPosition(bool insidePosition = false, int randomizePositionRadius = 20)
        {
            var nodes = insidePosition ? RoundManager.Instance.insideAINodes : RoundManager.Instance.outsideAINodes;
            var result = nodes[Random.Range(0, nodes.Length - 1)].transform.position;
            if (randomizePositionRadius > 0)
            {
                result = RoundManager.Instance.GetRandomNavMeshPositionInRadius(result, randomizePositionRadius);
            }
            return result;
        }
    }
}
