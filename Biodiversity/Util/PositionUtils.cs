using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Util
{
    public static class PositionUtils
    {
        /// <summary>
        /// Gets a random position selected from the given nodes, seeded on rand
        /// </summary>
        /// <param name="rand">Created Random seed</param>
        /// <param name="nodes">The given nodes to calculate the position from</param>
        /// <param name="randomRadius">If greater than 0, will also sample the final calculated position on a NavMesh circle of the given radius</param>
        /// <param name="areaMask">Exclude specified NavMesh areas if not -1</param>
        /// <returns>Random position based on parameters</returns>
        public static Vector3 GetSeededMoonPosition(System.Random rand, GameObject[] nodes, float randomRadius = 20, int areaMask = -1)
        {
            Vector3 pos = nodes[rand.Next(0, nodes.Length)].transform.position;
            float y = pos.y;
            Quaternion quat = Quaternion.Euler((float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()), (float)(360f * rand.NextDouble()));
            Vector3 forward = quat * Vector3.forward;
            pos = (forward * (float)(randomRadius * rand.NextDouble())) + pos;
            pos.y = y;
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, randomRadius, areaMask))
            {
                return navHit.position;
            }
            return pos;
        }

        /// <summary>
        /// Gets a random position selected from the given nodes
        /// </summary>
        /// <param name="nodes">The given nodes to calculate the position from</param>
        /// <param name="randomRadius">If greater than 0, will also sample the final calculated position on a NavMesh circle of the given radius</param>
        /// <param name="areaMask">Exclude specified NavMesh areas if not -1</param>
        /// <returns>Random position based on parameters</returns>
        public static Vector3 GetRandomMoonPosition(GameObject[] nodes, float randomRadius = 20, int areaMask = -1)
        {
            Vector3 randomPoint = nodes[Random.Range(0, nodes.Length - 1)].transform.position;
            return GetRandomPositionNearPosition(randomPoint, randomRadius, areaMask);
        }

        /// <summary>
        /// Gets a random position neer the given position parameter
        /// </summary>
        /// <param name="position">The original position</param>
        /// <param name="randomRadius">If greater than 0, will also sample the final calculated position on a NavMesh circle of the given radius</param>
        /// <param name="areaMask">Exclude specified NavMesh areas if not -1</param>
        /// <returns>Random position based on parameters</returns>
        public static Vector3 GetRandomPositionNearPosition(Vector3 position, float randomRadius = 20, int areaMask = -1)
        {
            if (randomRadius > 0)
            {
                float positionY = position.y;
                Vector3 randomPosition = Random.insideUnitSphere * randomRadius + position;
                randomPosition.y = positionY;
                if (NavMesh.SamplePosition(randomPosition, out NavMeshHit navHit, randomRadius, areaMask))
                {
                    return navHit.position;
                }
            }
            return position;
        }

        /// <summary>
        /// Gets a position from the given nodes that is the closest to the given position
        /// </summary>
        /// <param name="nodes">The given nodes to calculate the position from</param>
        /// <param name="position">The original position</param>
        /// <returns>Closest node position to original position</returns>
        public static Vector3 GetClosestAINodePosition(GameObject[] nodes, Vector3 position)
        {
            return nodes.OrderBy((GameObject x) => Vector3.Distance(position, x.transform.position)).ToArray()[0].transform.position;
        }
    }
}
