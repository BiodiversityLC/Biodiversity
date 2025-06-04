using System.Collections.Generic;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

public class FakePlayerBodyRagdoll : NetworkBehaviour
{
    /// <summary>
    /// The skinned mesh renderer for the body mesh.
    /// </summary>
    [Tooltip("The SkinnedMeshRenderer component used to render the body mesh.")]
    [SerializeField] public SkinnedMeshRenderer bodyMeshRenderer;

    /// <summary>
    /// A list of body parts involved in the ragdoll physics.
    /// </summary>
    [Tooltip("List of BodyPart objects involved in the ragdoll physics.")]
    [SerializeField] private List<BodyPart> bodyParts = [];

    /// <summary>
    /// The maximum velocity allowed for limbs.
    /// </summary>
    [Tooltip("Maximum velocity allowed for limb movement.")]
    public float maxVelocity = 0.4f;
    
    /// <summary>
    /// Multiplier for the speed at which limbs move towards their targets.
    /// </summary>
    [Tooltip("Speed multiplier for limb movement towards target positions.")]
    public float speedMultiplier = 14f;

    /// <summary>
    /// Determines if limbs should match positions exactly with their targets.
    /// </summary>
    [Tooltip("If true, limbs will match positions exactly with their targets.")]
    public bool matchPositionExactly = true;
    
    /// <summary>
    /// Determines if limbs should interpolate before matching positions exactly.
    /// </summary>
    [Tooltip("If true, limbs will interpolate before matching positions exactly.")]
    public bool lerpBeforeMatchingPosition;

    /// <summary>
    /// The interval at which each body part's limb collider is re-enabled.
    /// </summary>
    [Tooltip("Interval (in seconds) which each body part's limb collider is re-enabled.")]
    public float resetInterval = 0.25f;
    
    /// <summary>
    /// The duration over which limbs interpolate movement.
    /// </summary>
    [Tooltip("Duration (in seconds) over which limbs interpolate movement.")]
    public float lerpDuration = 0.3f;
    
    /// <summary>
    /// The minimum distance at which force is applied to limbs.
    /// </summary>
    [Tooltip("Minimum distance before force is applied to limbs.")]
    public float forceMinDistance = 0.2f;
    
    /// <summary>
    /// The maximum distance at which force is applied to limbs.
    /// </summary>
    [Tooltip("Maximum distance for applying force to limbs.")]
    public float forceMaxDistance = 2.5f;
    
    /// <summary>
    /// The maximum distance allowed before a limb is reset.
    /// </summary>
    [Tooltip("Maximum distance allowed before a limb is reset.")]
    public float maxDistanceToReset = 4.0f;

    private bool _wasMatchingPosition;
    private bool _networkEventsSubscribed;

    private float _moveToExactPositionTimer;
    private float _restBodyPartsTimer;
    private float _lastReceivedTime;
    private const float PositionLerpTime = 0.1f;

    private Vector3 _forceDirection;
    private Vector3 _lastReceivedPosition;

    private Quaternion _lastReceivedRotation;
    
    private readonly NetworkVariable<Vector3> _networkPosition = new();
    private readonly NetworkVariable<Quaternion> _networkRotation = new();
    
    private readonly Dictionary<string, BodyPart> _bodyPartMap = new();

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void Start()
    {
        SubscribeToNetworkEvents();

        foreach (BodyPart bodyPart in bodyParts)
        {
            if (!string.IsNullOrEmpty(bodyPart.name))
                _bodyPartMap.Add(bodyPart.name, bodyPart);
        }
    }
    
    /// <summary>
    /// Updates limb positions and handles collision enabling.
    /// </summary>
    private void Update()
    {
        if (!IsOwner) return;

        for (int i = 0; i < bodyParts.Count; i++)
        {
            BodyPart bodyPart = bodyParts[i];
            if (!bodyPart.active)
                continue;
            
            if (bodyPart.attachedTo != null && matchPositionExactly)
                ResetBodyPositionIfTooFarFromAttachment(bodyPart);

            _restBodyPartsTimer += Time.deltaTime;
            if (_restBodyPartsTimer >= resetInterval)
            {
                _restBodyPartsTimer = 0f;
                EnableCollisionOnBodyParts();
            }
        }
    }

    /// <summary>
    /// Synchronizes position and rotation over the network.
    /// </summary>
    private void LateUpdate()
    {
        if (IsOwner)
        {
            for (int i = 0; i < bodyParts.Count; i++)
            {
                BodyPart bodyPart = bodyParts[i];
                if (!bodyPart.active)
                    continue;
                
                if (bodyPart.attachedTo == null || bodyPart.attachedTo.parent == transform)
                {
                    HandleDetachedLimb(bodyPart);
                }
                else
                {
                    HandleAttachedLimb(bodyPart);
                }
            }
            
            // Use interpolation to reduce network load
            // todo: copy the mirror network transform's logic and use that for more advanced interpolation stuff
            _networkPosition.Value = transform.position;
            _networkRotation.Value = transform.rotation;
        }
        else
        {
            float timeSinceLastUpdate = Time.time - _lastReceivedTime;
            float t = timeSinceLastUpdate / PositionLerpTime;

            transform.position = Vector3.Lerp(transform.position, _lastReceivedPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, _lastReceivedRotation, t);
        }
    }
    
    /// <summary>
    /// Handles a limb that is detached from its attachment point.
    /// </summary>
    /// <param name="bodyPart">The body part to handle.</param>
    private void HandleDetachedLimb(BodyPart bodyPart)
    {
        if (!IsOwner) return;
        
        _moveToExactPositionTimer = 0.0f;
        if (!_wasMatchingPosition) return;
        _wasMatchingPosition = false;

        bodyPart.limbRigidbody.velocity = Vector3.zero;
        bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
        bodyPart.limbRigidbody.ResetCenterOfMass();
        bodyPart.limbRigidbody.ResetInertiaTensor();
        bodyPart.limbRigidbody.freezeRotation = false;
        bodyPart.limbRigidbody.isKinematic = false;

        EnableCollisionOnBodyParts();
    }
    
    /// <summary>
    /// Handles a limb that is attached to a transform.
    /// </summary>
    /// <param name="bodyPart">The body part to handle.</param>
    private void HandleAttachedLimb(BodyPart bodyPart)
    {
        if (!IsOwner) return;
        if (matchPositionExactly)
        {
            if (lerpBeforeMatchingPosition && _moveToExactPositionTimer < lerpDuration)
            {
                _moveToExactPositionTimer += Time.deltaTime;
                speedMultiplier = 25f;
            }
            else
            {
                bodyPart.limbRigidbody.freezeRotation = true;
                bodyPart.limbRigidbody.isKinematic = true;
                bodyPart.limbRigidbody.transform.position = bodyPart.attachedTo.position;
                bodyPart.limbRigidbody.transform.rotation = bodyPart.attachedTo.rotation;
                return;
            }
        }

        ApplyForcesToLimb(bodyPart.limbRigidbody, bodyPart.attachedTo);
    }

    /// <summary>
    /// Applies forces to move a limb towards its target transform.
    /// </summary>
    /// <param name="limb">The limb's Rigidbody component.</param>
    /// <param name="target">The target transform to move towards.</param>
    private void ApplyForcesToLimb(Rigidbody limb, Transform target)
    {
        if (!IsOwner) return;
        
        _forceDirection = (target.position - limb.position).normalized;
        float distance = Vector3.Distance(target.position, limb.position);
        
        if (distance < forceMinDistance)
        {
            limb.velocity = Vector3.zero;
            return;
        }

        limb.AddForce(
            _forceDirection * (speedMultiplier * Mathf.Clamp(Vector3.Distance(target.position, limb.position),
                forceMinDistance, forceMaxDistance)),
            ForceMode.VelocityChange);

        if (limb.velocity.sqrMagnitude > maxVelocity)
        {
            limb.velocity = limb.velocity.normalized * maxVelocity;
        }
    }

    /// <summary>
    /// Resets the limb's position if it's too far from its attachment point.
    /// </summary>
    /// <param name="activeBodyPart">The active body part to check.</param>
    private void ResetBodyPositionIfTooFarFromAttachment(BodyPart activeBodyPart)
    {
        if (!IsOwner) return;
        
        for (int i = 0; i < bodyParts.Count; i++)
        {
            BodyPart bodyPart = bodyParts[i];
            if (Vector3.Distance(bodyPart.limbRigidbody.position, activeBodyPart.attachedTo.position) > maxDistanceToReset)
            {
                _restBodyPartsTimer = 0f;
                bodyPart.limbCollider.enabled = false;
            }
        }
    }

    /// <summary>
    /// Enables collision on all body parts.
    /// </summary>
    private void EnableCollisionOnBodyParts()
    {
        if (!IsOwner) return;

        for (int i = 0; i < bodyParts.Count; i++)
        {
            BodyPart bodyPart = bodyParts[i];
            bodyPart.limbCollider.enabled = true;
        }
    }

    /// <summary>
    /// Applies the player's suit material to the body mesh renderer.
    /// </summary>
    /// <param name="playerObjectId">The ID of the player object.</param>
    public void ApplySuitMaterial(int playerObjectId)
    {
        if (StartOfRound.Instance == null) return;

        PlayerControllerB playerScript = StartOfRound.Instance.allPlayerScripts[playerObjectId];
        bodyMeshRenderer.sharedMaterial =
            StartOfRound.Instance.unlockablesList.unlockables[playerScript.currentSuitID].suitMaterial;
        bodyMeshRenderer.renderingLayerMask = (uint)(513 | 1 << playerObjectId + 12);
    }

    /// <summary>
    /// Attaches a limb to a specified transform.
    /// </summary>
    /// <param name="bodyPartName">The name of the body part to attach.</param>
    /// <param name="transformToAttachTo">The transform to attach the limb to.</param>
    /// <param name="retainVelocity">Whether to retain the limb's current velocity.</param>
    public void AttachLimbToTransform(string bodyPartName, Transform transformToAttachTo, bool retainVelocity = false)
    {
        if (!IsOwner) return;
        if (!_bodyPartMap.TryGetValue(bodyPartName, out BodyPart bodyPart)) return;
        
        // If the given transform is null, detach the limb instead as a failsafe
        if (transformToAttachTo == null)
        {
            // BiodiversityPlugin.LogVerbose($"The given transform is null, cannot attach transform to body part {bodyPartName}");
            DetachLimbFromTransform(bodyPartName);
            return;
        }
        
        bodyPart.limbRigidbody.isKinematic = true;
        if (!retainVelocity)
        {
            bodyPart.limbRigidbody.velocity = Vector3.zero;
            bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
        }
        
        bodyPart.attachedTo = transformToAttachTo;
        bodyPart.active = true;
    }

    /// <summary>
    /// Detaches a limb from its current transform.
    /// </summary>
    /// <param name="bodyPartName">The name of the body part to detach.</param>
    /// <param name="retainVelocity">Whether to retain the limb's current velocity.</param>
    public void DetachLimbFromTransform(string bodyPartName, bool retainVelocity = false)
    {
        if (!IsOwner) return;
        if (!_bodyPartMap.TryGetValue(bodyPartName, out BodyPart bodyPart)) return;

        bodyPart.active = false;
        bodyPart.attachedTo = null;

        if (!retainVelocity)
        {
            bodyPart.limbRigidbody.velocity = Vector3.zero;
            bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
        }

        bodyPart.limbRigidbody.isKinematic = false;
    }
    
    /// <summary>
    /// Called when the network position variable changes.
    /// </summary>
    /// <param name="oldPosition">The old position value.</param>
    /// <param name="newPosition">The new position value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnNetworkPositionChanged(Vector3 oldPosition, Vector3 newPosition)
    {
        _lastReceivedPosition = newPosition;
        _lastReceivedTime = Time.time;
    }

    /// <summary>
    /// Called when the network rotation variable changes.
    /// </summary>
    /// <param name="oldRotation">The old rotation value.</param>
    /// <param name="newRotation">The new rotation value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnNetworkRotationChanged(Quaternion oldRotation, Quaternion newRotation)
    {
        _lastReceivedRotation = newRotation;
        _lastReceivedTime = Time.time;
    }
    
    /// <summary>
    /// Subscribes to network variable change events.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        if (IsOwner || _networkEventsSubscribed) return;
        _networkPosition.OnValueChanged += OnNetworkPositionChanged;
        _networkRotation.OnValueChanged += OnNetworkRotationChanged;
        _networkEventsSubscribed = true;
    }

    /// <summary>
    /// Unsubscribes from network variable change events.
    /// </summary>
    private void UnsubscribeFromNetworkEvents()
    {
        if (IsOwner || !_networkEventsSubscribed) return;
        _networkPosition.OnValueChanged -= OnNetworkPositionChanged;
        _networkRotation.OnValueChanged -= OnNetworkRotationChanged;
        _networkEventsSubscribed = false;
    }
}