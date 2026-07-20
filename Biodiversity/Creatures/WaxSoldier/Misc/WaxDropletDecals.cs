using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.WaxSoldier.Misc;

[RequireComponent(typeof(ParticleSystem))]
public sealed class WaxDropletDecals : MonoBehaviour
{
    [Header("Decal")]
    [SerializeField] private GameObject waxSplatterDecalPrefab;
    [SerializeField] private LayerMask validSurfaceLayers = ~0;

    [Header("Placement")]
    [Tooltip("Distance to offset the decal away from the hit surface along its normal. Use a small positive value to reduce clipping or z-fighting.")]
    [SerializeField, Min(0f)]
    private float surfaceOffset = 0.002f;

    [Tooltip("Minimum and maximum diameter, in Unity units, used to randomly size each spawned wax decal. X is the minimum; Y is the maximum.")]
    [SerializeField]
    private Vector2 diameterRange = new(0.3f, 0.5f);

    [Tooltip("Minimum and maximum height-to-width ratio applied to each decal. A value of 1 preserves the texture's proportions; lower values compress it and higher values elongate it.")]
    [SerializeField]
    private Vector2 aspectRange = new(0.8f, 1.2f);

    [Tooltip("Depth of the decal projector volume along its local Z-axis. Keep this shallow to avoid projecting onto nearby or opposite surfaces.")]
    [SerializeField, Min(0.001f)]
    private float projectionDepth = 0.025f;

    [Header("Lifetime")]
    [Tooltip("Set to zero to keep decals permanently.")]
    [SerializeField, Min(0f)] private float decalLifetime = 60f;

    [Header("Limits")]
    [SerializeField, Min(1)] private int maxDecalsPerCallback = 4;

    private ParticleSystem particles;
    private readonly List<ParticleCollisionEvent> collisionEvents = [];

    private void Awake()
    {
        particles = GetComponent<ParticleSystem>();

        if (!waxSplatterDecalPrefab)
            BiodiversityPlugin.Logger.LogError($"{nameof(WaxDropletDecals)} on {name} has no decal prefab assigned.");
    }

    private void OnParticleCollision(GameObject other)
    {
        BiodiversityPlugin.LogVerbose($"In {nameof(OnParticleCollision)}");
        if (!waxSplatterDecalPrefab)
            return;

        if (!IsValidSurface(other))
            return;

        int eventCount = particles.GetCollisionEvents(other, collisionEvents);
        int spawnCount = Mathf.Min(eventCount, maxDecalsPerCallback);

        for (int i = 0; i < spawnCount; i++)
        {
            ParticleCollisionEvent collision = collisionEvents[i];

            Vector3 position = collision.intersection + collision.normal * surfaceOffset;

            // HDRP Decal Projectors project along local Z; point local forward into the struck surface
            Quaternion rotation = Quaternion.LookRotation(-collision.normal);

            // Prevent every wax splatter having an identical orientation
            rotation *= Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);

            GameObject decalObject = Instantiate(waxSplatterDecalPrefab, position, rotation);
            if (decalObject.TryGetComponent(out DecalProjector projector))
            {
                float diameter = Random.Range(
                    diameterRange.x,
                    diameterRange.y);

                float aspect = Random.Range(
                    aspectRange.x,
                    aspectRange.y);

                projector.size = new Vector3(
                    diameter,
                    diameter * aspect,
                    projectionDepth);
            }
            else
            {
                BiodiversityPlugin.Logger.LogWarning($"{waxSplatterDecalPrefab.name} has no DecalProjector component.");
            }

            if (decalLifetime > 0f)
                Destroy(decalObject, decalLifetime);
        }
    }

    /// <summary>
    /// Determines whether the specified GameObject is on a layer included in <see cref="validSurfaceLayers"/>.
    /// </summary>
    /// <param name="surface"> The GameObject whose layer should be checked. </param>
    /// <returns>
    /// <c>true</c> if the GameObject's layer is included in
    /// <see cref="validSurfaceLayers"/>; otherwise, <c>false</c>.
    /// </returns>
    private bool IsValidSurface(GameObject surface)
    {
        int surfaceBit = 1 << surface.layer;
        return (validSurfaceLayers.value & surfaceBit) != 0;
    }
}