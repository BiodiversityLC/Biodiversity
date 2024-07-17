using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Biodiversity.Creatures.Aloe;

public class FakePlayerBodyRagdoll : NetworkBehaviour
{
    private string _ragdollId;
    private ManualLogSource _mls;
    
    /// <summary>
    /// This enum is for the vanilla DeadPlayerInfo body parts variable
    /// </summary>
    public enum DeadPlayerBodyParts
    {
        Neck = 0,
        LowerRightArm = 1,
        LowerLeftArm = 2,
        RightShin = 3,
        LeftShin = 4,
        Torso = 5,
        Root = 6,
        RightThigh = 7,
        LeftThigh = 8,
        UpperLeftArm = 9,
        UpperRightArm = 10,
    }

#pragma warning disable 0649
    public SkinnedMeshRenderer bodyMeshRenderer;
#pragma warning restore 0649

    public List<BodyPart> bodyParts = [];

    public float maxVelocity = 0.4f;
    public float speedMultiplier = 14f;

    public bool matchPositionExactly = true;
    public bool lerpBeforeMatchingPosition;

    public float resetInterval = 0.25f;
    public float lerpDuration = 0.3f;
    public float forceMinDistance = 0.2f;
    public float forceMaxDistance = 2.5f;
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

    private void Awake()
    {
        _ragdollId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Player Ragdoll {_ragdollId}");
    }

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
        LogDebug("Fake player body ragdoll spawned");
    }
    
    private void Update()
    {
        if (!IsOwner) return;
        
        foreach (BodyPart bodyPart in bodyParts.Where(bodyPart => bodyPart.active))
        {
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

    private void LateUpdate()
    {
        if (IsOwner)
        {
            foreach (BodyPart bodyPart in bodyParts.Where(bodyPart => bodyPart.active))
            {
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

    private void ResetBodyPositionIfTooFarFromAttachment(BodyPart activeBodyPart)
    {
        if (!IsOwner) return;
        
        foreach (BodyPart bodyPart in bodyParts.Where(
                     bodyPart => Vector3.Distance(
                                     bodyPart.limbRigidbody.position,
                                     activeBodyPart.attachedTo.position) >
                                 maxDistanceToReset))
        {
            _restBodyPartsTimer = 0f;
            bodyPart.limbCollider.enabled = false;
        }
    }

    private void EnableCollisionOnBodyParts()
    {
        if (!IsOwner) return;
        
        foreach (BodyPart bodyPart in bodyParts)
        {
            bodyPart.limbCollider.enabled = true;
        }
    }

    public void ApplySuitMaterial(int playerObjectId)
    {
        if (StartOfRound.Instance == null) return;

        PlayerControllerB playerScript = StartOfRound.Instance.allPlayerScripts[playerObjectId];
        bodyMeshRenderer.sharedMaterial =
            StartOfRound.Instance.unlockablesList.unlockables[playerScript.currentSuitID].suitMaterial;
        bodyMeshRenderer.renderingLayerMask = (uint)(513 | 1 << playerObjectId + 12);
    }

    public void AttachLimbToTransform(string bodyPartName, Transform transformToAttachTo, bool retainVelocity = false)
    {
        if (!IsOwner) return;
        
        LogDebug($"In {nameof(AttachLimbToTransform)}");
        BodyPart bodyPart = bodyParts.Find(bp => bp.name == bodyPartName);
        if (bodyPart == null)
        {
            LogDebug($"Body part name {bodyPartName} does not exist, cannot attach limb.");
            return;
        }
        
        // If the given transform is null, detach the limb instead as a failsafe
        if (transformToAttachTo == null)
        {
            LogDebug($"The given transform is null, cannot attach transform to body part {bodyPartName}");
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

    public void DetachLimbFromTransform(string bodyPartName, bool retainVelocity = false)
    {
        if (!IsOwner) return;
        
        LogDebug($"In {nameof(DetachLimbFromTransform)}");
        BodyPart bodyPart = bodyParts.Find(bp => bp.name == bodyPartName);
        if (bodyPart == null) return;

        bodyPart.active = false;
        bodyPart.attachedTo = null;

        if (!retainVelocity)
        {
            bodyPart.limbRigidbody.velocity = Vector3.zero;
            bodyPart.limbRigidbody.angularVelocity = Vector3.zero;
        }

        bodyPart.limbRigidbody.isKinematic = false;
    }
    
    private void OnNetworkPositionChanged(Vector3 oldPosition, Vector3 newPosition)
    {
        _lastReceivedPosition = newPosition;
        _lastReceivedTime = Time.time;
    }

    private void OnNetworkRotationChanged(Quaternion oldRotation, Quaternion newRotation)
    {
        _lastReceivedRotation = newRotation;
        _lastReceivedTime = Time.time;
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (IsOwner || _networkEventsSubscribed) return;
        _networkPosition.OnValueChanged += OnNetworkPositionChanged;
        _networkRotation.OnValueChanged += OnNetworkRotationChanged;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (IsOwner || !_networkEventsSubscribed) return;
        _networkPosition.OnValueChanged -= OnNetworkPositionChanged;
        _networkRotation.OnValueChanged -= OnNetworkRotationChanged;
        _networkEventsSubscribed = false;
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log.</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo($"{msg}");
        #endif
    }
}