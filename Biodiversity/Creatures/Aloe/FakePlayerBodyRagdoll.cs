using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.SerializableTypes;
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
    [SerializeField] private SkinnedMeshRenderer bodyMeshRenderer;
#pragma warning restore 0649

    public List<BodyPart> bodyParts = [];

    private Vector3 _forceDirection;

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

    private float _moveToExactPositionTimer;
    private float _restBodyPartsTimer;
    
    private readonly NetworkVariable<Vector3> _networkPosition = new();
    private readonly NetworkVariable<Quaternion> _networkRotation = new();

    private void Awake()
    {
        _ragdollId = Guid.NewGuid().ToString();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Player Ragdoll {_ragdollId}");
    }

    private void Start()
    {
        LogDebug("Fake player body ragdoll spawned");
    }
    
    private void Update()
    {
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

        // Todo: use interpolation/extrapolation to reduce network load
        if (IsOwner)
        {
            _networkPosition.Value = transform.position;
            _networkRotation.Value = transform.rotation;
        }
        else
        {
            transform.position = _networkPosition.Value;
            transform.rotation = _networkRotation.Value;
        }
    }

    private void HandleDetachedLimb(BodyPart bodyPart)
    {
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

    // public void SetRagdollPositionSafely(Vector3 newPosition, bool disableSpecialEffects = false)
    // {
    //     transform.position = newPosition + Vector3.up * 2.5f;
    //     foreach (Rigidbody bodyPart in bodyParts)
    //     {
    //         bodyPart.velocity = Vector3.zero;
    //     }
    // }
    
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