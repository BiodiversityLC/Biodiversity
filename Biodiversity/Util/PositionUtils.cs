using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Util
{
    public static class PositionUtils
    {
        public static Vector3 GetSeededMoonPosition(System.Random rand, bool insidePosition = false, float radius = 20f)
        {
            GameObject[] nodes = insidePosition ? RoundManager.Instance.insideAINodes : RoundManager.Instance.outsideAINodes;
            Vector3 pos = nodes[rand.Next(0, nodes.Length)].transform.position;
            float y = pos.y;
            Quaternion quat = Quaternion.Euler((float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()));
            Vector3 forward = quat * Vector3.forward;
            pos = (forward * (float)(radius * rand.NextDouble())) + pos;
            pos.y = y;
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, radius, -1))
            {
                return navHit.position;
            }
            return pos;
        }

        public static Vector3 GetRandomMoonPosition(bool insidePosition = false, bool ignoreUnderwater = true, int randomizePositionRadius = 20)
        {
            GameObject[] nodes = insidePosition ? RoundManager.Instance.insideAINodes : (ignoreUnderwater ? RoundManager.Instance.outsideAIDryNodes : RoundManager.Instance.outsideAINodes);
            Vector3 randomPoint = nodes[Random.Range(0, nodes.Length - 1)].transform.position;
            return GetRandomPositionNearPosition(randomPoint, ignoreUnderwater, randomizePositionRadius);
        }

        public static Vector3 GetRandomPositionNearPosition(Vector3 position, bool ignoreUnderwater = true, int randomizePositionRadius = 20)
        {
            if (randomizePositionRadius > 0)
            {
                int areaMask = ignoreUnderwater ? NavMesh.AllAreas & ~(1 << 12) : -1;
                float positionY = position.y;
                Vector3 randomPosition = Random.insideUnitSphere * randomizePositionRadius + position;
                randomPosition.y = positionY;
                if (NavMesh.SamplePosition(randomPosition, out NavMeshHit navHit, randomizePositionRadius, areaMask))
                {
                    return navHit.position;
                }
            }
            return position;
        }
    }
}
